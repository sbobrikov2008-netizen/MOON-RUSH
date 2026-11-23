using System.Collections.Generic;
using UnityEngine;

internal static class PressureRoute
{
    public static Vector3[] Find(Terrain terrain, BuildingInstance first, BuildingInstance second,
        IReadOnlyList<BuildingInstance> buildings, IReadOnlyList<PressureCorridor> corridors,
        HashSet<Transform> occupied, out Transform start, out Transform end)
    {
        start = end = null;
        if (!first.TryGetComponent(out LunarBaseModule a) || !second.TryGetComponent(out LunarBaseModule b)) return null;
        Vector3[] best = null;
        float bestLength = float.PositiveInfinity;
        foreach (Transform port in a.ConnectionPorts)
        foreach (Transform incoming in b.ConnectionPorts)
        {
            if (port == null || incoming == null || occupied.Contains(port) || occupied.Contains(incoming)) continue;
            Vector3 exit = port.position + port.forward * 4.6f;
            Vector3 entry = incoming.position + incoming.forward * 4.6f;
            Vector3 along = entry - exit;
            along.y = 0f;
            if (along.sqrMagnitude < 4f) continue;
            Vector3 side = Vector3.Cross(Vector3.up, along.normalized);
            float length = bestLength;
            Vector3[] candidate = best;
            Consider(new[] { port.position, exit, entry, incoming.position });
            for (int shift = -4; shift <= 4; shift++)
            {
                if (shift == 0) continue;
                Vector3 offset = side * (shift * 9f);
                Consider(new[] { port.position, exit, Vector3.Lerp(exit, entry, .5f) + offset, entry, incoming.position });
                Consider(new[] { port.position, exit, Vector3.Lerp(exit, entry, .26f) + offset,
                    Vector3.Lerp(exit, entry, .74f) + offset, entry, incoming.position });
            }
            if (length >= bestLength) continue;
            bestLength = length;
            best = candidate;
            start = port;
            end = incoming;

            void Consider(Vector3[] path)
            {
                float total = 0f;
                for (int index = 1; index < path.Length; index++)
                {
                    Vector3 from = path[index - 1], to = path[index];
                    Vector3 delta = to - from;
                    float segment = delta.magnitude;
                    if (segment < 1.4f || Mathf.Abs(delta.y) > segment * .23f) return;
                    if (index > 1 && Vector3.Dot((from - path[index - 2]).normalized, delta.normalized) < -.06f) return;
                    total += segment;
                    if (total >= length) return;
                    int samples = Mathf.CeilToInt(segment);
                    for (int step = 0; step <= samples; step++)
                    {
                        Vector3 point = Vector3.Lerp(from, to, step / (float)samples);
                        Vector3 local = point - terrain.transform.position;
                        Vector3 extents = terrain.terrainData.size;
                        if (local.x < 2f || local.z < 2f || local.x > extents.x - 2f || local.z > extents.z - 2f)
                            return;
                        float rise = local.y - terrain.SampleHeight(point);
                        if (rise < 1.38f || rise > 7f) return;
                        foreach (BuildingInstance building in buildings)
                        {
                            if (building == null || building.Definition.kind == BuildingKind.PressureCorridor) continue;
                            if ((building == first && index == 1) || (building == second && index == path.Length - 1)) continue;
                            if (building == first || building == second)
                            {
                                Collider footprint = building.GetComponent<Collider>();
                                if (footprint != null && Vector3.Distance(point, footprint.ClosestPoint(point)) < 1.65f)
                                    return;
                            }
                            else
                            {
                                Vector3 deltaBuilding = point - building.transform.position;
                                deltaBuilding.y = 0f;
                                float radius = building.Definition.footprintRadius + 2.1f;
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
                        foreach (PressureCorridor corridor in corridors)
                            if (corridor != null && corridor.Occupies(point, 3.85f)) return;
                    }
                }
                length = total;
                candidate = path;
            }
        }
        return best;
    }
}
