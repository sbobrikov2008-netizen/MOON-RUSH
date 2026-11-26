using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class ColonyIndustry
{
    internal enum Resource { Ore, Water, Refined }
    internal sealed class Pipe
    {
        public BuildingInstance Source, Target;
        public GameObject Visual;
        public Resource Cargo;
        public Vector3[] Path;
    }

    sealed class Store
    {
        public double Observed, Ore, Water, Refined, Fuel;
    }

    readonly Dictionary<BuildingInstance, Store> stores = new Dictionary<BuildingInstance, Store>();
    readonly List<Pipe> pipes = new List<Pipe>();
    readonly Dictionary<BuildingInstance,int> connectedInputs = new Dictionary<BuildingInstance,int>();
    public IReadOnlyList<Pipe> Pipes => pipes;
    public int Samples { get; private set; }
    public int Surveys { get; private set; }
    public float ResearchRemaining { get; private set; }
    public bool EarthResearch { get; private set; }
    double sampleProgress;

    public bool BeginResearch(bool labPowered, bool labExists, bool hasLandingPad)
    {
        if (Samples == 0 || ResearchRemaining > 0f || (labExists ? !labPowered : !hasLandingPad)) return false;
        Samples--;
        EarthResearch = !labExists;
        ResearchRemaining = EarthResearch ? 95f : 26f;
        return true;
    }

    public void Tick(float seconds, IReadOnlyList<BuildingInstance> buildings)
    {
        bool labPowered = false;
        connectedInputs.Clear();
        foreach(Pipe link in pipes)
        {
            if (link.Source==null || link.Target==null || link.Visual==null) continue;
            connectedInputs.TryGetValue(link.Target,out int flags);
            connectedInputs[link.Target]=flags | (1<<(int)link.Cargo);
        }
        foreach (BuildingInstance building in buildings)
        {
            if (building == null || !building.isActiveAndEnabled) continue;
            switch (building.Kind)
            {
                case BuildingKind.ScienceLab:
                    labPowered |= building.HasPower;
                    break;
                case BuildingKind.MaterialsStation:
                case BuildingKind.WaterStation:
                    Store input = Get(building);
                    double delta = Math.Max(0d, building.ProducedUnits - input.Observed);
                    input.Observed = building.ProducedUnits;
                    if (building.Kind == BuildingKind.MaterialsStation)
                    {
                        input.Ore += delta;
                        sampleProgress += delta;
                        int found = (int)sampleProgress;
                        Samples += found;
                        sampleProgress -= found;
                    }
                    else input.Water += delta;
                    break;
                case BuildingKind.Refinery:
                case BuildingKind.FuelWorks:
                    int required=building.Kind==BuildingKind.Refinery ? 1<<(int)Resource.Ore :
                        (1<<(int)Resource.Refined) | (1<<(int)Resource.Water);
                    bool connected=building.HasPower && connectedInputs.TryGetValue(building,out int available) &&
                        (available & required)==required;
                    Store process = Get(building);
                    bool hasFeed=building.Kind==BuildingKind.Refinery ? process.Ore>.0001 :
                        process.Refined>.0001 && process.Water>.0001;
                    if(building.TryGetComponent(out LunarBaseModule hall)) hall.SetProcessing(connected && hasFeed);
                    if (!connected) break;
                    double capacity = seconds * (building.Kind == BuildingKind.Refinery ? 2d : 1.25d) / 60d;
                    if (building.Kind == BuildingKind.Refinery)
                    {
                        double amount = Math.Min(capacity, process.Ore);
                        process.Ore -= amount;
                        process.Refined += amount;
                    }
                    else
                    {
                        double amount = Math.Min(capacity, Math.Min(process.Refined, process.Water));
                        process.Refined -= amount;
                        process.Water -= amount;
                        process.Fuel += amount;
                    }
                    break;
            }
        }
        foreach (Pipe pipe in pipes)
        {
            if (pipe.Source == null || pipe.Target == null || pipe.Visual == null) continue;
            bool powered=pipe.Source.HasPower && pipe.Target.HasPower;
            if (pipe.Cargo==Resource.Ore && pipe.Visual.TryGetComponent(out IndustrialPipeVisual belt))
                belt.SetMoving(powered && Get(pipe.Source).Ore>.0001 && seconds>0f);
            if (!powered) continue;
            Store a = Get(pipe.Source), b = Get(pipe.Target);
            double flow = 4d * seconds / 60d;
            switch (pipe.Cargo)
            {
                case Resource.Ore: Transfer(ref a.Ore, ref b.Ore, flow); break;
                case Resource.Water: Transfer(ref a.Water, ref b.Water, flow); break;
                default: Transfer(ref a.Refined, ref b.Refined, flow); break;
            }
        }
        if (ResearchRemaining > 0f && (EarthResearch || labPowered))
        {
            ResearchRemaining = Mathf.Max(0f, ResearchRemaining - seconds);
            if (ResearchRemaining == 0f) Surveys++;
        }

        static void Transfer(ref double a, ref double b, double maximum)
        {
            double amount = Math.Min(a, maximum);
            a -= amount;
            b += amount;
        }
    }

    public string Stock(BuildingInstance item)
    {
        Store stock = Get(item);
        switch (item.Kind)
        {
            case BuildingKind.MaterialsStation: return $"Реголит в буфере: {stock.Ore:F1}";
            case BuildingKind.WaterStation: return $"Вода в буфере: {stock.Water:F1}";
            case BuildingKind.Refinery: return $"Реголит: {stock.Ore:F1} · Концентрат: {stock.Refined:F1}";
            case BuildingKind.FuelWorks: return $"Концентрат: {stock.Refined:F1} · Вода: {stock.Water:F1} · Топливо: {stock.Fuel:F1}";
            case BuildingKind.ScienceLab: return $"Образцы: {Samples} · Исследований: {Surveys}";
            default: return string.Empty;
        }
    }

    public static bool TryCargo(BuildingKind from, BuildingKind to, out Resource cargo)
    {
        cargo = from == BuildingKind.WaterStation ? Resource.Water :
            from == BuildingKind.Refinery ? Resource.Refined : Resource.Ore;
        return (from == BuildingKind.MaterialsStation && to == BuildingKind.Refinery) ||
            ((from == BuildingKind.WaterStation || from == BuildingKind.Refinery) && to == BuildingKind.FuelWorks);
    }

    public bool Free(BuildingInstance source, BuildingInstance destination, Resource cargo)
    {
        foreach (Pipe pipe in pipes)
            if (pipe.Source == source || (pipe.Target == destination && pipe.Cargo == cargo)) return false;
        return true;
    }

    public void Add(BuildingInstance source, BuildingInstance destination, Resource cargo, GameObject visual, Vector3[] path) =>
        pipes.Add(new Pipe { Source = source, Target = destination, Cargo = cargo, Visual = visual, Path = path });

    public void Remove(Pipe pipe) => pipes.Remove(pipe);
    public void Forget(BuildingInstance building) => stores.Remove(building);
    public void CollectSample() => Samples++;

    public static bool HasOutput(BuildingKind kind) =>
        kind == BuildingKind.MaterialsStation || kind == BuildingKind.WaterStation || kind == BuildingKind.Refinery;

    public static Vector3 Socket(BuildingKind kind, bool output, Resource cargo)
    {
        if (kind == BuildingKind.Refinery) return output ? new Vector3(10.05f, .86f, 2.9f) : new Vector3(-10.05f, .86f, 1.9f);
        if (kind == BuildingKind.FuelWorks) return cargo == Resource.Water ?
            new Vector3(10.05f, .86f, 2.9f) : new Vector3(-10.05f, .86f, 1.9f);
        return new Vector3(9.04f, .86f, -4.5f);
    }

    Store Get(BuildingInstance building)
    {
        if (!stores.TryGetValue(building, out Store store))
        {
            store = new Store();
            stores.Add(building, store);
        }
        return store;
    }
}
