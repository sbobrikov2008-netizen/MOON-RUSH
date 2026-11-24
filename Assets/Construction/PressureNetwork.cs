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
            corridor.BuildRoute(route,terrain);
            corridor.FinishGeometry();
        }
    }

    internal static HashSet<BuildingInstance> BuildingsReachableFrom(BuildingInstance command,
        IReadOnlyList<Edge> edges)
    {
        var visited = new HashSet<BuildingInstance>();
        if (command == null || !command.isActiveAndEnabled) return visited;

        var adjacencies = new Dictionary<BuildingInstance, List<BuildingInstance>>();
        var links = new HashSet<PressureCorridor>();
        foreach (Edge edge in edges)
        {
            PressureCorridor corridor = edge.Corridor;
            if (!IsConnected(corridor) || !links.Add(corridor)) continue;
            Add(corridor.First, corridor.Second);
            Add(corridor.Second, corridor.First);
        }
        var queue = new Queue<BuildingInstance>();
        visited.Add(command);
        queue.Enqueue(command);
        while (queue.Count > 0)
        {
            BuildingInstance current = queue.Dequeue();
            if (!adjacencies.TryGetValue(current, out var next)) continue;
            foreach (BuildingInstance building in next)
                if (visited.Add(building)) queue.Enqueue(building);
        }
        return visited;

        void Add(BuildingInstance a, BuildingInstance b)
        {
            if (!adjacencies.TryGetValue(a, out var list))
            {
                list = new List<BuildingInstance>();
                adjacencies.Add(a, list);
            }
            list.Add(b);
        }
    }

    internal static bool IsConnected(PressureCorridor corridor) => corridor != null && corridor.isActiveAndEnabled &&
        PortAttached(corridor.First, corridor.StartPort) && PortAttached(corridor.Second, corridor.EndPort);

    static bool PortAttached(BuildingInstance building, Transform port) =>
        building != null && building.isActiveAndEnabled && building.Status == BuildingStatus.Built &&
        port != null && port.IsChildOf(building.transform);

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
            if (!IsConnected(edge.Corridor)) continue;
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
