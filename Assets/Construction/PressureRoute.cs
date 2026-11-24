using System.Collections.Generic;
using UnityEngine;

internal static class PressureRoute
{
    internal readonly struct Anchor
    {
        public readonly BuildingInstance Building;
        public readonly Transform Port;
        public readonly PressureCorridor Host;
        public readonly Vector3 Position, Normal;

        public Anchor(BuildingInstance building, Transform port)
        {
            Building = building;
            Port = port;
            Host = null;
            Position = port.position;
            Normal = port.forward;
        }

        Anchor(PressureCorridor host, Vector3 position, Vector3 normal)
        {
            Building = null;
            Port = null;
            Host = host;
            Position = position;
            Normal = normal;
        }

        public bool Exists => Port != null || Host != null;

        public static Anchor OnCorridor(PressureCorridor corridor, Vector3 pointer)
        {
            float closest = float.PositiveInfinity;
            Anchor result = default;
            for (int i = 1; i < corridor.Route.Count; i++)
            {
                Vector3 a = corridor.Route[i - 1], b = corridor.Route[i];
                Vector3 point = Vector3.Lerp(a, b, PressureCorridor.Projection(pointer, a, b));
                float distance = (point - pointer).sqrMagnitude;
                if (distance >= closest) continue;
                closest = distance;
                result = new Anchor(corridor, point, Vector3.Cross(Vector3.up, PressureCorridor.Horizontal(b - a)));
            }
            return closest <= 16f ? result : default;
        }
    }

    public static Vector3[] Find(Terrain terrain, Anchor source, Anchor target,
        IReadOnlyList<BuildingInstance> buildings, IReadOnlyList<PressureCorridor> corridors,
        IReadOnlyList<PressureNetwork.Edge> edges, out string failure)
    {
        failure = "Нет маршрута: препятствие, крутой склон или недостаточно места для стыковки";
        if (!source.Exists || !target.Exists || (target.Position - source.Position).sqrMagnitude < 16f ||
            (source.Building != null && source.Building == target.Building)) return null;
        Vector3[] best = null;
        float bestLength = float.PositiveInfinity;
        bool heightConflict = false;
        int approachesAtStart = source.Host == null ? 1 : 2;
        int approachesAtEnd = target.Host == null ? 1 : 2;
        for (int a = 0; a < approachesAtStart; a++)
        for (int b = 0; b < approachesAtEnd; b++)
        {
            Vector3 outwards = source.Normal * (a == 0 ? 1f : -1f);
            Vector3 inwards = target.Normal * (b == 0 ? 1f : -1f);
            Vector3 exit = source.Position + outwards * (source.Host == null ? 4.2f : 5.5f);
            Vector3 entry = target.Position + inwards * (target.Host == null ? 4.2f : 5.5f);
            Vector3 along = PressureCorridor.Horizontal(entry - exit);
            if (along == Vector3.zero) continue;
            Vector3 side = Vector3.Cross(Vector3.up, along);
            Consider(new[] { source.Position, target.Position });
            Consider(new[] { source.Position, exit, entry, target.Position });
            // Include an elbow along either departure axis as well as rounded detours.
            foreach (Vector3 axis in new[] {outwards, inwards})
            {
                Vector3 elbow = exit + axis * Vector3.Dot(entry - exit, axis);
                elbow.y = (exit.y + entry.y) * .5f;
                Consider(new[] { source.Position, exit, elbow, entry, target.Position });
            }
            for (int shift = -5; shift <= 5; shift++)
            {
                if (shift == 0) continue;
                Vector3 offset = side * (shift * 6f);
                Consider(new[] { source.Position, exit, Vector3.Lerp(exit, entry, .5f) + offset, entry, target.Position });
                Consider(new[] { source.Position, exit, Vector3.Lerp(exit, entry, .26f) + offset,
                    Vector3.Lerp(exit, entry, .74f) + offset, entry, target.Position });
            }

            void Consider(Vector3[] path)
            {
                if (Vector3.Dot(PressureCorridor.Horizontal(path[1] - path[0]), outwards) < .9995f ||
                    Vector3.Dot(PressureCorridor.Horizontal(path[path.Length - 2] - path[path.Length - 1]), inwards) < .9995f)
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
                            if (building == source.Building || building == target.Building)
                            {
                                if ((building == source.Building && Vector3.Distance(point, path[0]) < 4.61f) ||
                                    (building == target.Building && Vector3.Distance(point, path[path.Length - 1]) < 4.61f)) continue;
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
                if (!PressureNetwork.RouteFits(route, corridors, edges, terrain, source.Host, target.Host, ref heightConflict)) return;
                bestLength = total;
                best = route;
            }
        }
        if (best == null && heightConflict) failure = "Нельзя состыковать переходы: уровни пола не совпадают";
        return best;
    }
}
