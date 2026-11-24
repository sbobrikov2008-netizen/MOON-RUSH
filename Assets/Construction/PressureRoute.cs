using System.Collections.Generic;
using UnityEngine;

internal static class PressureRoute
{
    internal readonly struct Anchor
    {
        public readonly BuildingInstance Building;
        public readonly Transform Port;
        public readonly Vector3 Position, Normal;

        public Anchor(BuildingInstance building, Transform port)
        {
            Building = building;
            Port = port;
            Position = port.position;
            Normal = port.forward;
        }

        public bool Exists => Port != null;
    }

    public static Vector3[] Find(Terrain terrain, Anchor source, Anchor target,
        IReadOnlyList<BuildingInstance> buildings, IReadOnlyList<PressureCorridor> corridors,
        out string failure)
    {
        failure = "Нет маршрута: препятствие, крутой склон или недостаточно места для стыковки";
        if (!source.Exists || !target.Exists || (target.Position - source.Position).sqrMagnitude < 16f ||
            (source.Building != null && source.Building == target.Building)) return null;
        Vector3[] best = null;
        float bestLength = float.PositiveInfinity;
        Vector3 outwards = source.Normal;
        Vector3 inwards = target.Normal;
        Vector3 exit = source.Position + outwards * 4.2f;
        Vector3 entry = target.Position + inwards * 4.2f;
        Vector3 along = PressureCorridor.Horizontal(entry - exit);
        if (along == Vector3.zero) along = outwards;
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
                if (segment < 3.45f || axis == Vector3.zero) return;
                if (index > 1 && Vector3.Dot(PressureCorridor.Horizontal(from - path[index - 2]), axis) < -.035f) return;
                total += new Vector2(delta.x, delta.z).magnitude;
                if (total >= bestLength) return;
                int samples = Mathf.CeilToInt(segment);
                for (int step = 0; step <= samples; step++)
                {
                    Vector3 point = Vector3.Lerp(from, to, step / (float)samples);
                    foreach (BuildingInstance building in buildings)
                    {
                        if (building == null || building.Definition.kind == BuildingKind.PressureCorridor) continue;
                        if (building == source.Building || building == target.Building)
                        {
                            if ((building == source.Building && Vector3.Distance(point, path[0]) < 4.61f) ||
                                (building == target.Building && Vector3.Distance(point, path[path.Length - 1]) < 4.61f)) continue;
                            BoxCollider footprint = building.GetComponent<BoxCollider>();
                            if (footprint != null)
                            {
                                Vector3 local = building.transform.InverseTransformPoint(point) - footprint.center;
                                if (Mathf.Abs(local.x) < footprint.size.x * .5f + 1.52f &&
                                    Mathf.Abs(local.z) < footprint.size.z * .5f + 1.52f) return;
                            }
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
            if (!PressureNetwork.RouteFits(route, corridors) || !FitFloor(terrain, route, out Vector3[] fitted)) return;
            bestLength = total;
            best = fitted;
        }
        return best;
    }
    // Fit the lowest profile that clears the full deck and can still meet both fixed door sills.
    internal static bool FitFloor(Terrain terrain, Vector3[] route, out Vector3[] fitted)
    {
        const float maximumGrade = .18f;
        const float groundMargin = .23f;
        var points = new List<Vector3> { route[0] };
        var lower = new List<float> { float.NegativeInfinity };
        var upper = new List<float> { float.PositiveInfinity };
        var corners = new List<int>();
        fitted = null;
        for (int span = 1; span < route.Length; span++)
        {
            Vector3 a = route[span - 1], b = route[span];
            Vector3 axis = PressureCorridor.Horizontal(b - a);
            if (span > 1 && Vector3.Dot(axis, PressureCorridor.Horizontal(a - route[span - 2])) < .9995f)
                corners.Add(points.Count - 1);
            Vector3 right = Vector3.Cross(Vector3.up, axis);
            int count = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / .72f));
            for (int step = 0; step <= count; step++)
            {
                Vector3 point = Vector3.Lerp(a, b, step / (float)count);
                float high = float.NegativeInfinity, low = float.PositiveInfinity;
                for (int across = -2; across <= 2; across++)
                {
                    Vector3 sample = point + right * (across * .78f);
                    Vector3 local = sample - terrain.transform.position;
                    Vector3 size = terrain.terrainData.size;
                    float u = local.x / size.x, v = local.z / size.z;
                    int holes = terrain.terrainData.holesResolution;
                    if (u < 0f || u >= 1f || v < 0f || v >= 1f ||
                        terrain.terrainData.IsHole((int)(u * holes), (int)(v * holes))) return false;
                    float y = terrain.transform.position.y + terrain.SampleHeight(sample);
                    high = Mathf.Max(high, y);
                    low = Mathf.Min(low, y);
                }
                float portDistance = Mathf.Min(FlatDistance(point, route[0]), FlatDistance(point, route[route.Length - 1]));
                float clearance = Mathf.Lerp(.035f, groundMargin, Mathf.Clamp01(portDistance / 1.7f));
                float minimum = high - PressureCorridor.Floor + clearance;
                float maximum = low - PressureCorridor.Floor + 1.52f;
                if (step == 0)
                {
                    int last = points.Count - 1;
                    lower[last] = Mathf.Max(lower[last], minimum);
                    upper[last] = Mathf.Min(upper[last], maximum);
                }
                else
                {
                    points.Add(point);
                    lower.Add(minimum);
                    upper.Add(maximum);
                }
            }
        }

        int lastIndex = points.Count - 1;
        if (points[0].y < lower[0] || points[lastIndex].y < lower[lastIndex]) return false;
        upper[0] = lower[0] = points[0].y;
        upper[lastIndex] = lower[lastIndex] = points[lastIndex].y;
        // Turns need a level collar wide enough to carry the shared curved shell.
        for (int pass = 0; pass <= corners.Count; pass++)
        {
            foreach (int corner in corners)
            {
                int left = corner, right = corner;
                while (left > 1 && Run(left - 1, corner) < 1.95f) left--;
                while (right < lastIndex - 1 && Run(right + 1, corner) < 1.95f) right++;
                float level = lower[corner];
                for (int i = left; i <= right; i++) level = Mathf.Max(level, lower[i]);
                for (int i = left; i <= right; i++) lower[i] = level;
            }
            for (int i = 1; i <= lastIndex; i++)
                lower[i] = Mathf.Max(lower[i], lower[i - 1] - Run(i, i - 1) * maximumGrade);
            for (int i = lastIndex - 1; i >= 0; i--)
                lower[i] = Mathf.Max(lower[i], lower[i + 1] - Run(i, i + 1) * maximumGrade);
        }
        for (int i = 0; i <= lastIndex; i++)
        {
            if (lower[i] > upper[i] + .001f) return false;
            Vector3 point = points[i];
            point.y = lower[i];
            points[i] = point;
        }
        var compact = new List<Vector3> { points[0] };
        for (int i = 1; i < lastIndex; i++)
            if (Vector3.Cross((points[i] - points[i - 1]).normalized, (points[i + 1] - points[i]).normalized)
                .sqrMagnitude > .0000001f) compact.Add(points[i]);
        compact.Add(points[lastIndex]);
        fitted = compact.ToArray();
        return true;

        float Run(int a, int b) => FlatDistance(points[a], points[b]);

        static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
    }
}
