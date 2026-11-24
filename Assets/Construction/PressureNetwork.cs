using System.Collections.Generic;
using UnityEngine;

public static class PressureNetwork
{
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
        public readonly List<(Vector3, Vector3)> Mouths = new List<(Vector3, Vector3)>();
    }

    internal static bool RouteFits(Vector3[] path, IReadOnlyList<PressureCorridor> corridors)
    {
        for (int i = 1; i < path.Length; i++)
        {
            Vector3 a = path[i - 1], b = path[i];
            for (int earlier = 1; earlier < i - 1; earlier++)
                if (TooClose(a, b, path[earlier - 1], path[earlier], 2.82f)) return false;
            foreach (PressureCorridor corridor in corridors)
            for (int j = 1; j < corridor.Route.Count; j++)
                if (TooClose(a, b, corridor.Route[j - 1], corridor.Route[j], 3.16f)) return false;
        }
        return true;

        static bool TooClose(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float width) =>
            Intersection(a, b, c, d, out _, out _) ||
            Mathf.Min(PressureCorridor.DistanceToSegment(a, c, d), PressureCorridor.DistanceToSegment(b, c, d),
                PressureCorridor.DistanceToSegment(c, a, b), PressureCorridor.DistanceToSegment(d, a, b)) < width;
    }

    public static void Rebuild(IReadOnlyList<PressureCorridor> corridors, Terrain terrain, List<Edge> edges)
    {
        edges.Clear();
        foreach (PressureCorridor corridor in corridors)
        {
            var route = corridor.Route;
            for (int i = 1; i < route.Count; i++)
                edges.Add(new Edge(route[i - 1], route[i], corridor));
            if (corridor.ShellRenderer != null) continue;
            corridor.BeginGeometry();
            var corners = new Junction[route.Count];
            for (int i = 1; i < route.Count - 1; i++)
            {
                Vector3 incoming = PressureCorridor.Horizontal(route[i] - route[i - 1]);
                Vector3 outgoing = PressureCorridor.Horizontal(route[i + 1] - route[i]);
                if (Vector3.Dot(incoming, outgoing) >= .9995f) continue;
                corners[i] = new Junction { Center = route[i], Radius = 1.62f };
            }
            for (int i = 1; i < route.Count; i++)
            {
                Junction a = corners[i - 1], b = corners[i];
                Vector3 from = route[i - 1], to = route[i];
                Vector3 direction = PressureCorridor.Horizontal(to - from);
                float run = FlatDistance(from, to);
                if (run < .001f) continue;
                float startTrim = a == null ? 0f : a.Radius;
                float endTrim = b == null ? 0f : b.Radius;
                if (startTrim + endTrim > run * .94f)
                {
                    float factor = run * .94f / (startTrim + endTrim);
                    startTrim *= factor;
                    endTrim *= factor;
                }
                Vector3 start = Vector3.Lerp(from, to, startTrim / run);
                Vector3 end = Vector3.Lerp(from, to, 1f - endTrim / run);
                corridor.AddSpan(start, end, terrain, i == 1, i == route.Count - 1);
                if (a != null) a.Mouths.Add((start, direction));
                if (b != null) b.Mouths.Add((end, -direction));
            }
            foreach (Junction bend in corners)
                if (bend != null && bend.Mouths.Count == 2)
                    corridor.AddJunction(bend.Center, bend.Mouths, terrain);
            corridor.FinishGeometry();
        }
    }

    // Corridor nodes are bidirectional. Ports of a building share an interior node.
    public static bool TryFindPath(BuildingInstance from, BuildingInstance to, IReadOnlyList<Edge> edges,
        out Vector3[] waypoints)
    {
        waypoints = System.Array.Empty<Vector3>();
        if (from == null || to == null) return false;
        if (from == to) return true;
        var neighbors = new Dictionary<Vector3, List<Vector3>>();
        var interiors = new Dictionary<BuildingInstance, List<Vector3>>();
        var seenCorridors = new HashSet<PressureCorridor>();
        foreach (Edge edge in edges)
        {
            Link(edge.From, edge.To);
            if (!seenCorridors.Add(edge.Corridor)) continue;
            Register(edge.Corridor.First, edge.Corridor.Route[0]);
            Register(edge.Corridor.Second, edge.Corridor.Route[edge.Corridor.Route.Count - 1]);
        }
        if (!interiors.TryGetValue(from, out var start) || !interiors.TryGetValue(to, out var finish)) return false;
        var destinations = new HashSet<Vector3>(finish);
        var visited = new HashSet<Vector3>(start);
        var previous = new Dictionary<Vector3, Vector3>();
        var queue = new Queue<Vector3>(start);
        while (queue.Count > 0)
        {
            Vector3 next = queue.Dequeue();
            if (destinations.Contains(next))
            {
                var route = new List<Vector3> { next + Vector3.up * PressureCorridor.Floor };
                while (previous.TryGetValue(next, out Vector3 parent))
                {
                    next = parent;
                    route.Add(next + Vector3.up * PressureCorridor.Floor);
                }
                route.Reverse();
                waypoints = route.ToArray();
                return true;
            }
            if (!neighbors.TryGetValue(next, out var adjacent)) continue;
            foreach (Vector3 point in adjacent)
            {
                if (!visited.Add(point)) continue;
                previous.Add(point, next);
                queue.Enqueue(point);
            }
        }
        return false;

        void Register(BuildingInstance building, Vector3 port)
        {
            if (building == null) return;
            if (!interiors.TryGetValue(building, out var ports))
            {
                ports = new List<Vector3>();
                interiors.Add(building, ports);
            }
            if (ports.Count > 0) Link(ports[0], port);
            ports.Add(port);
        }

        void Link(Vector3 a, Vector3 b)
        {
            AddNeighbor(a, b);
            AddNeighbor(b, a);
        }

        void AddNeighbor(Vector3 a, Vector3 b)
        {
            if (!neighbors.TryGetValue(a, out var list))
            {
                list = new List<Vector3>();
                neighbors.Add(a, list);
            }
            list.Add(b);
        }
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
