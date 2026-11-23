using System.Collections.Generic;
using UnityEngine;

public static class PressureNetwork
{
    const float JunctionRadius = 3.5f;
    const float Merge = .08f;
    const float HeightTolerance = .22f;
    const float MinimumCross = .78f;

    public readonly struct Edge
    {
        public readonly Vector3 From, To;
        public readonly PressureCorridor Corridor;

        internal Edge(Vector3 from, Vector3 to, PressureCorridor corridor)
        {
            From = from;
            To = to;
            Corridor = corridor;
        }
    }

    sealed class Junction
    {
        public Vector3 Center;
        public float Radius;
        public PressureCorridor Owner;
        public readonly List<(Vector3, Vector3)> Mouths = new List<(Vector3, Vector3)>();
    }

    internal static bool RouteFits(Vector3[] path, IReadOnlyList<PressureCorridor> corridors,
        IReadOnlyList<Edge> networkEdges, Terrain terrain, PressureCorridor branch, ref bool heightConflict)
    {
        var junctions = new List<Vector3>();
        for (int i = 1; i < path.Length; i++)
        {
            Vector3 a = path[i - 1], b = path[i];
            foreach (PressureCorridor corridor in corridors)
            for (int j = 1; j < corridor.Route.Count; j++)
            {
                Vector3 c = corridor.Route[j - 1], d = corridor.Route[j];
                if (Intersection(a, b, c, d, out float t, out float u))
                {
                    Vector3 p = Vector3.Lerp(a, b, t), q = Vector3.Lerp(c, d, u);
                    if (Mathf.Abs(p.y - q.y) > HeightTolerance)
                    {
                        heightConflict = true;
                        return false;
                    }
                    bool isBranch = corridor == branch && i == path.Length - 1 && Vector3.Distance(p, b) < Merge;
                    if ((!isBranch && Mathf.Min(FlatDistance(p, a), FlatDistance(p, b)) < JunctionRadius + 1.7f) ||
                        (isBranch && FlatDistance(p, a) < JunctionRadius + 1.7f) ||
                        Mathf.Min(FlatDistance(q, c), FlatDistance(q, d)) < JunctionRadius + 1.7f ||
                        Mathf.Abs(Cross(PressureCorridor.Horizontal(b - a), PressureCorridor.Horizontal(d - c))) < MinimumCross)
                        return false;
                    foreach (Vector3 other in junctions)
                        if (FlatDistance(p, other) < JunctionRadius * 2f + .35f) return false;
                    foreach (Edge edge in networkEdges)
                    {
                        if (edge.Corridor != corridor) continue;
                        if ((IsInteriorCut(edge.From, corridor) && FlatDistance(p, edge.From) < JunctionRadius * 2f + .35f) ||
                            (IsInteriorCut(edge.To, corridor) && FlatDistance(p, edge.To) < JunctionRadius * 2f + .35f))
                            return false;
                    }
                    Vector3 center = (p + q) * .5f;
                    if (!FitsTerrain(center, terrain)) return false;
                    junctions.Add(p);
                }
                else if (Mathf.Min(PressureCorridor.DistanceToSegment(a, c, d), PressureCorridor.DistanceToSegment(b, c, d),
                    PressureCorridor.DistanceToSegment(c, a, b), PressureCorridor.DistanceToSegment(d, a, b)) < 3.04f)
                    return false;
            }
        }
        if (branch == null) return true;
        foreach (Vector3 point in junctions)
            if (FlatDistance(point, path[path.Length - 1]) < Merge) return true;
        return false;
    }

    static bool FitsTerrain(Vector3 center, Terrain terrain)
    {
        for (int x = -3; x <= 3; x++)
        for (int z = -3; z <= 3; z++)
        {
            if (x * x + z * z > 17) continue;
            Vector3 sample = center + new Vector3(x, 0f, z);
            float rise = sample.y - terrain.transform.position.y - terrain.SampleHeight(sample);
            if (rise < 1.42f || rise > 7f) return false;
        }
        return true;
    }

    static bool IsInteriorCut(Vector3 point, PressureCorridor corridor)
    {
        foreach (Vector3 control in corridor.Route)
            if (FlatDistance(control, point) < Merge) return false;
        return true;
    }

    public static void Rebuild(IReadOnlyList<PressureCorridor> corridors, Terrain terrain, List<Edge> edges)
    {
        edges.Clear();
        var nodes = new List<Junction>();
        var splits = new List<(float order, Junction node)>[corridors.Count];
        for (int i = 0; i < corridors.Count; i++)
        {
            PressureCorridor corridor = corridors[i];
            corridor.BeginGeometry();
            splits[i] = new List<(float, Junction)>();
            for (int k = 0; k < corridor.Route.Count; k++)
            {
                Vector3 point = corridor.Route[k];
                bool interior = k > 0 && k < corridor.Route.Count - 1;
                float radius = interior && Vector3.Dot(
                    PressureCorridor.Horizontal(point - corridor.Route[k - 1]),
                    PressureCorridor.Horizontal(corridor.Route[k + 1] - point)) < .9995f ? 1.62f : 0f;
                splits[i].Add((k, GetNode(point, radius, corridor)));
            }
        }

        for (int i = 0; i < corridors.Count; i++)
        for (int j = i + 1; j < corridors.Count; j++)
        {
            var a = corridors[i].Route;
            var b = corridors[j].Route;
            for (int k = 1; k < a.Count; k++)
            for (int l = 1; l < b.Count; l++)
            {
                if (!Intersection(a[k - 1], a[k], b[l - 1], b[l], out float t, out float u)) continue;
                Vector3 p = Vector3.Lerp(a[k - 1], a[k], t), q = Vector3.Lerp(b[l - 1], b[l], u);
                if (Mathf.Abs(p.y - q.y) > HeightTolerance) continue;
                Junction node = GetNode((p + q) * .5f, JunctionRadius, corridors[i]);
                AddCut(splits[i], k - 1 + t, node);
                AddCut(splits[j], l - 1 + u, node);
            }
        }

        for (int i = 0; i < corridors.Count; i++)
        {
            var path = splits[i];
            path.Sort((a, b) => a.order.CompareTo(b.order));
            for (int j = 1; j < path.Count; j++)
            {
                Junction a = path[j - 1].node, b = path[j].node;
                Vector3 delta = b.Center - a.Center;
                Vector3 direction = PressureCorridor.Horizontal(delta);
                if (direction == Vector3.zero) continue;
                float length = FlatDistance(a.Center, b.Center);
                Vector3 from = a.Center + delta * (a.Radius / length);
                Vector3 to = b.Center - delta * (b.Radius / length);
                corridors[i].AddSpan(from, to, terrain,
                    j == 1 && corridors[i].StartPort != null, j == path.Count - 1 && corridors[i].EndPort != null);
                if (a.Radius > 0f) a.Mouths.Add((from, direction));
                if (b.Radius > 0f) b.Mouths.Add((to, -direction));
                edges.Add(new Edge(a.Center, b.Center, corridors[i]));
            }
        }
        foreach (Junction node in nodes)
            if (node.Mouths.Count >= 2) node.Owner.AddJunction(node.Center, node.Mouths, terrain);
        foreach (PressureCorridor corridor in corridors) corridor.FinishGeometry();

        Junction GetNode(Vector3 point, float radius, PressureCorridor owner)
        {
            foreach (Junction node in nodes)
            {
                if (FlatDistance(node.Center, point) > Merge || Mathf.Abs(node.Center.y - point.y) > HeightTolerance) continue;
                if (radius > node.Radius)
                {
                    node.Center = point;
                    node.Radius = radius;
                }
                return node;
            }
            var added = new Junction { Center = point, Radius = radius, Owner = owner };
            nodes.Add(added);
            return added;
        }
    }

    static void AddCut(List<(float order, Junction node)> cuts, float order, Junction node)
    {
        foreach (var cut in cuts)
            if (cut.node == node) return;
        cuts.Add((order, node));
    }

    internal static bool Intersection(Vector3 a, Vector3 b, Vector3 c, Vector3 d, out float t, out float u)
    {
        Vector3 p = b - a, q = d - c, gap = c - a;
        float denominator = Cross(p, q);
        t = u = 0f;
        if (Mathf.Abs(denominator) < .0001f) return false;
        t = Cross(gap, q) / denominator;
        u = Cross(gap, p) / denominator;
        return t >= -.00001f && t <= 1.00001f && u >= -.00001f && u <= 1.00001f;
    }

    static float Cross(Vector3 a, Vector3 b) => a.x * b.z - a.z * b.x;
    static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
}
