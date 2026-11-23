using System.Collections.Generic;
using UnityEngine;

internal static class PressureRoute
{
    public static Vector3[] Find(Terrain terrain, BuildingInstance first, BuildingInstance second,
        PressureCorridor branch, Vector3 pointer, IReadOnlyList<BuildingInstance> buildings,
        IReadOnlyList<PressureCorridor> corridors, IReadOnlyList<PressureNetwork.Edge> edges,
        HashSet<Transform> occupied, out Transform start, out Transform end, out string failure)
    {
        start = end = null;
        failure = "Нет маршрута: препятствие, крутой склон или недостаточно места для стыковки";
        if (!first.TryGetComponent(out LunarBaseModule source)) return null;
        var targets = new List<(Transform port, Vector3 position, Vector3 normal)>();
        if (branch != null)
        {
            float closest = float.PositiveInfinity;
            (Vector3 point, Vector3 normal) target = default;
            for (int i = 1; i < branch.Route.Count; i++)
            {
                Vector3 a = branch.Route[i - 1], b = branch.Route[i];
                if (Vector3.Distance(a, b) < 10.6f) continue;
                Vector3 point = Vector3.Lerp(a, b, PressureCorridor.Projection(pointer, a, b));
                float distance = (point - pointer).sqrMagnitude;
                if (distance >= closest) continue;
                closest = distance;
                target = (point, Vector3.Cross(Vector3.up, PressureCorridor.Horizontal(b - a)));
            }
            if (closest > 16f) return null;
            targets.Add((null, target.point, target.normal));
            targets.Add((null, target.point, -target.normal));
        }
        else if (second != null && second.TryGetComponent(out LunarBaseModule destination))
        {
            foreach (Transform port in destination.ConnectionPorts)
                if (port != null && !occupied.Contains(port))
                    targets.Add((port, port.position, port.forward));
        }

        Vector3[] best = null;
        float bestLength = float.PositiveInfinity;
        bool heightConflict = false;
        foreach (Transform port in source.ConnectionPorts)
        foreach (var target in targets)
        {
            if (port == null || occupied.Contains(port)) continue;
            Vector3 exit = port.position + port.forward * 4.6f;
            Vector3 entry = target.position + target.normal * (branch == null ? 4.6f : 6f);
            Vector3 along = PressureCorridor.Horizontal(entry - exit);
            if (along == Vector3.zero) continue;
            Vector3 side = Vector3.Cross(Vector3.up, along);
            float previousBest = bestLength;
            Consider(new[] { port.position, target.position });
            Consider(new[] { port.position, exit, entry, target.position });
            for (int shift = -4; shift <= 4; shift++)
            {
                if (shift == 0) continue;
                Vector3 offset = side * (shift * 9f);
                Consider(new[] { port.position, exit, Vector3.Lerp(exit, entry, .5f) + offset, entry, target.position });
                Consider(new[] { port.position, exit, Vector3.Lerp(exit, entry, .26f) + offset,
                    Vector3.Lerp(exit, entry, .74f) + offset, entry, target.position });
            }
            if (bestLength >= previousBest) continue;
            start = port;
            end = target.port;

            void Consider(Vector3[] path)
            {
                if (Vector3.Dot(PressureCorridor.Horizontal(path[1] - path[0]), port.forward) < .9995f ||
                    Vector3.Dot(PressureCorridor.Horizontal(path[path.Length - 2] - path[path.Length - 1]), target.normal) < .9995f)
                    return;
                float total = 0f;
                for (int index = 1; index < path.Length; index++)
                {
                    Vector3 from = path[index - 1], to = path[index];
                    Vector3 delta = to - from;
                    float segment = delta.magnitude;
                    Vector3 axis = PressureCorridor.Horizontal(delta);
                    if (segment < 3.65f || Mathf.Abs(delta.y) > segment * .17f) return;
                    if (index > 1 && Vector3.Dot(PressureCorridor.Horizontal(from - path[index - 2]), axis) < -.035f) return;
                    total += segment;
                    if (total >= bestLength) return;
                    int samples = Mathf.CeilToInt(segment);
                    Vector3 right = Vector3.Cross(Vector3.up, axis);
                    for (int step = 0; step <= samples; step++)
                    {
                        Vector3 point = Vector3.Lerp(from, to, step / (float)samples);
                        for (int across = -1; across <= 1; across++)
                        {
                            Vector3 surface = point + right * across * 1.53f;
                            Vector3 local = surface - terrain.transform.position;
                            Vector3 extents = terrain.terrainData.size;
                            if (local.x < 2f || local.z < 2f || local.x > extents.x - 2f || local.z > extents.z - 2f)
                                return;
                            float rise = local.y - terrain.SampleHeight(surface);
                            if (rise < 1.45f || rise > 7f) return;
                        }
                        foreach (BuildingInstance building in buildings)
                        {
                            if (building == null || building.Definition.kind == BuildingKind.PressureCorridor) continue;
                            if (building == first || building == second)
                            {
                                if ((building == first && Vector3.Distance(point, path[0]) < 4.61f) ||
                                    (building == second && Vector3.Distance(point, path[path.Length - 1]) < 4.61f)) continue;
                                Collider footprint = building.GetComponent<Collider>();
                                if (footprint != null && Vector3.Distance(point, footprint.ClosestPoint(point)) < 1.65f) return;
                                continue;
                            }
                            Vector3 deltaBuilding = point - building.transform.position;
                            deltaBuilding.y = 0f;
                            float radius = building.Definition.footprintRadius + 3.6f;
                            if (deltaBuilding.sqrMagnitude < radius * radius) return;
                            if (building.TryGetComponent<LunarLandingPad>(out _))
                            {
                                Vector3 relative = building.transform.InverseTransformPoint(point);
                                Rect ramp = LunarLandingPad.RampFootprint;
                                if (relative.x > ramp.xMin - 2.1f && relative.x < ramp.xMax + 2.1f &&
                                    relative.z > ramp.yMin - 2.1f && relative.z < ramp.yMax + 2.1f) return;
                            }
                        }
                    }
                }

                var controls = new List<Vector3> { path[0] };
                for (int i = 1; i < path.Length - 1; i++)
                    if (Vector3.Dot((path[i] - path[i - 1]).normalized, (path[i + 1] - path[i]).normalized) < .999999f)
                        controls.Add(path[i]);
                controls.Add(path[path.Length - 1]);
                Vector3[] route = controls.ToArray();
                if (!PressureNetwork.RouteFits(route, corridors, edges, terrain, branch, ref heightConflict)) return;
                bestLength = total;
                best = route;
            }
        }
        if (best == null && heightConflict) failure = "Нельзя состыковать переходы: уровни пола не совпадают";
        return best;
    }
}
