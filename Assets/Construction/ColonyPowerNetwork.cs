using System.Collections.Generic;

internal static class ColonyPowerNetwork
{
    public static void Recalculate(BuildingInstance command, IReadOnlyList<BuildingInstance> buildings,
        IReadOnlyList<PressureNetwork.Edge> edges, IReadOnlyDictionary<BuildingInstance, SolarPowerCable> sources)
    {
        HashSet<BuildingInstance> reachable = PressureNetwork.BuildingsReachableFrom(command, edges);
        bool generated = false;
        foreach (var source in sources)
        {
            SolarPowerCable cable = source.Value;
            if (source.Key == null || source.Key.Kind != BuildingKind.SolarStation || !source.Key.isActiveAndEnabled ||
                cable == null || cable.Command != command || !cable.IsTransmitting) continue;
            generated = true;
            break;
        }

        foreach (BuildingInstance building in buildings)
        {
            if (building == null) continue;
            bool connected = reachable.Contains(building);
            bool powered = connected && generated;
            if (building.Kind == BuildingKind.SolarStation)
            {
                sources.TryGetValue(building, out SolarPowerCable cable);
                connected = command != null && command.isActiveAndEnabled && cable != null && cable.Command == command;
                powered = connected && building.isActiveAndEnabled && cable.IsTransmitting;
            }
            else if (building.Kind == BuildingKind.LandingPad)
            {
                connected = command != null && command.isActiveAndEnabled &&
                    sources.TryGetValue(building,out SolarPowerCable line) && line != null &&
                    line.Command == command && line.IsTransmitting;
                powered = connected && generated;
            }
            else if (building.TryGetComponent(out PressureCorridor corridor))
            {
                connected = PressureNetwork.IsConnected(corridor) && reachable.Contains(corridor.First);
                powered = connected && generated;
            }
            building.SetPower(powered ? BuildingPowerStatus.Powered :
                connected ? BuildingPowerStatus.Unpowered : BuildingPowerStatus.Disconnected);
        }
    }
}
