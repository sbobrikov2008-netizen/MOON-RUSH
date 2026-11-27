using System;
using System.Collections.Generic;

internal static class ColonyPowerNetwork
{
    internal readonly struct Balance
    {
        public readonly float Generation, Demand, Served;

        public Balance(float generation, float demand, float served)
        {
            Generation = generation;
            Demand = demand;
            Served = served;
        }

        public float Deficit => Math.Max(0f, Demand - Generation);
    }

    public static Balance Recalculate(BuildingInstance command, IReadOnlyList<BuildingInstance> buildings,
        IReadOnlyList<PressureNetwork.Edge> edges, IReadOnlyDictionary<BuildingInstance, SolarPowerCable> sources,
        IEnumerable<(BuildingInstance a, BuildingInstance b)> peers = null,
        Func<BuildingInstance, float> sunlight = null, ISet<BuildingInstance> disabled = null)
    {
        var electric = new Dictionary<BuildingInstance, List<BuildingInstance>>();
        foreach (var source in sources)
        {
            SolarPowerCable cable = source.Value;
            if (cable != null && cable.IsTransmitting) Link(source.Key, cable.Command);
        }
        if (peers != null)
            foreach (var peer in peers) Link(peer.a, peer.b);

        var connected = new HashSet<BuildingInstance>();
        var queue = new Queue<BuildingInstance>();
        if (command != null && command.isActiveAndEnabled)
        {
            connected.Add(command);
            queue.Enqueue(command);
        }
        while (queue.Count != 0)
        {
            BuildingInstance node = queue.Dequeue();
            if (!electric.TryGetValue(node, out var neighbors)) continue;
            foreach (BuildingInstance neighbor in neighbors)
                if (connected.Add(neighbor)) queue.Enqueue(neighbor);
        }

        var pressure = PressureNetwork.BuildingsReachableFrom(command, edges);
        float supply = 0f, demand = 0f, served = 0f;
        var consumers = new List<BuildingInstance>();
        var corridorMeters = new Dictionary<PressureCorridor, float>();
        foreach (var edge in edges)
        {
            if(edge.Corridor==null) continue;
            corridorMeters.TryGetValue(edge.Corridor, out float length);
            corridorMeters[edge.Corridor] = length + UnityEngine.Vector3.Distance(edge.From, edge.To);
        }
        foreach (BuildingInstance building in buildings)
        {
            if (building == null || !building.isActiveAndEnabled) continue;
            if (building.Kind == BuildingKind.LandingPad)
            {
                building.SetPower(BuildingPowerStatus.Powered);
                continue;
            }
            bool inGrid = connected.Contains(building) || pressure.Contains(building);
            if (building.Kind == BuildingKind.MaterialsStation || building.Kind == BuildingKind.Refinery ||
                building.Kind == BuildingKind.FuelWorks) inGrid = pressure.Contains(building);
            if (building.TryGetComponent(out PressureCorridor corridor))
                inGrid = PressureNetwork.IsConnected(corridor) && pressure.Contains(corridor.First);
            if (!inGrid)
            {
                building.SetPower(BuildingPowerStatus.Disconnected,Load(building));
                continue;
            }
            bool off = building.Kind == BuildingKind.SolarStation && disabled != null && disabled.Contains(building);
            if (off)
            {
                building.SetPower(BuildingPowerStatus.SwitchedOff,Load(building));
                continue;
            }
            if (building.Kind == BuildingKind.SolarStation)
                supply += building.Definition.solarCapacityKw * Clamp01(sunlight?.Invoke(building) ?? 1f);
            consumers.Add(building);
            demand += Load(building);
        }
        consumers.Sort((a, b) =>
        {
            if (a == command) return b == command ? 0 : -1;
            if (b == command) return 1;
            int priority = a.Definition.powerPriority.CompareTo(b.Definition.powerPriority);
            return priority != 0 ? priority : a.GetInstanceID().CompareTo(b.GetInstanceID());
        });

        bool higherPriorityShed = false;
        int shedPriority = int.MaxValue;
        foreach (BuildingInstance consumer in consumers)
        {
            float watts = Load(consumer);
            bool powered = (consumer == command || (command != null && command.HasPower)) && supply > 0f && served + watts <= supply + .0001f &&
                (!higherPriorityShed || consumer.Definition.powerPriority <= shedPriority);
            if (powered) served += watts;
            else
            {
                higherPriorityShed = true;
                shedPriority = Math.Min(shedPriority, consumer.Definition.powerPriority);
            }
            consumer.SetPower(powered ? BuildingPowerStatus.Powered : BuildingPowerStatus.Unpowered,watts);
        }
        return new Balance(supply, demand, served);

        float Load(BuildingInstance building)
        {
            float load = building.Definition.powerDemandKw;
            if (building.TryGetComponent(out PressureCorridor corridor) && corridorMeters.TryGetValue(corridor, out float meters))
                load *= Math.Max(1f, meters / 20f);
            return load;
        }

        void Link(BuildingInstance a, BuildingInstance b)
        {
            if (a == null || b == null || !a.isActiveAndEnabled || !b.isActiveAndEnabled) return;
            Add(a, b);
            Add(b, a);
        }

        void Add(BuildingInstance a, BuildingInstance b)
        {
            if (!electric.TryGetValue(a, out var neighbors))
            {
                neighbors = new List<BuildingInstance>();
                electric.Add(a, neighbors);
            }
            neighbors.Add(b);
        }

        static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
    }
}
