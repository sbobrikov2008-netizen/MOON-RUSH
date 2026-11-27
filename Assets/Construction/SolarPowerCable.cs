using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class SolarPowerCable : MonoBehaviour
{
    internal const float SolarPortZ = 14.58f;
    const float Pitch = 2f;
    const float CableRadius = .09f;
    [SerializeField] bool breakerOn = true;
    Mesh mesh;
    internal BuildingInstance Command { get; private set; }
    internal bool IsBreakerOn => breakerOn;
    internal bool IsTransmitting => breakerOn && isActiveAndEnabled && Command != null && Command.isActiveAndEnabled;

    internal void ToggleBreaker() => breakerOn = !breakerOn;
    IReadOnlyList<Vector3> points;
    IReadOnlyList<Vector3> renderedPoints;
    Vector3 Terminal => points[points.Count - 1];

    internal static Vector3[] Plan(Terrain terrain, Vector3 origin, Quaternion facing,
        float stationRadius, BuildingInstance command, IReadOnlyList<BuildingInstance> buildings,
        IEnumerable<SolarPowerCable> cables = null, BuildingInstance consumer = null,
        Vector3? localSocket = null, Vector3? localDirection = null, int searchLimit = 6500)
    {
        Vector3 exitDirection = facing * (localDirection ?? Vector3.forward);
        Vector3 departure = origin + facing * (localSocket ?? new Vector3(0f, 1.12f, SolarPortZ));
        Vector3 entry = origin + exitDirection * (stationRadius + 2f);
        Vector3 commandPosition = command.transform.position;
        Vector3[] best = null;
        float shortest = float.PositiveInfinity;

        var commandSockets = new[] {
            (position: new Vector3(0f, .62f, -10.40f), direction: Vector3.back),
            (position: new Vector3(-10.40f, .74f, -3.65f), direction: Vector3.left),
            (position: new Vector3(10.40f, .74f, -3.65f), direction: Vector3.right) };
        var solarSockets = new[] {
            (position: SolarArrayLayout.RearSocket, direction: Vector3.back),
            (position: SolarArrayLayout.SideSocket(-1), direction: Vector3.left),
            (position: SolarArrayLayout.SideSocket(1), direction: Vector3.right) };
        foreach (var socket in command.Kind == BuildingKind.SolarStation ? solarSockets : commandSockets)
        {
            if (!FindSocket(socket.position, socket.direction, out Vector3 terminal)) continue;
            Vector3 outward = command.transform.rotation * socket.direction;
            Vector3 approach = terminal + outward * (command.Definition.footprintRadius + 1.2f -
                Vector3.Dot(terminal - commandPosition, outward));
            if (!Clear(entry, departure, ignoreStation: true) || !Clear(approach, terminal, ignoreCommand: true) ||
                !Clear(entry, entry) || !Clear(approach, approach)) continue;
            List<Vector3> route = Trace(entry, approach);
            if (route == null) continue;
            float length = Vector3.Distance(departure, entry) + Vector3.Distance(approach, terminal);
            for (int i = 1; i < route.Count; i++) length += Vector3.Distance(route[i], route[i - 1]);
            if (length >= shortest) continue;
            shortest = length;
            route.Insert(0, departure);
            route.Add(terminal);
            best = Drape(route);
        }
        return best;

        bool FindSocket(Vector3 local, Vector3 normal, out Vector3 terminal)
        {
            terminal = default;
            Vector3 across = Vector3.Cross(Vector3.up, normal);
            foreach (float offset in new[] { 0f, .87f, -.87f, 1.74f, -1.74f, 2.61f, -2.61f, 3.48f, -3.48f })
            {
                Vector3 socket = local + across * offset;
                if ((normal == Vector3.back ? Mathf.Abs(socket.x) : Mathf.Abs(socket.z)) > 3.72f) continue;
                Vector3 candidate = command.transform.TransformPoint(socket);
                bool occupied = false;
                if (cables != null)
                    foreach (SolarPowerCable cable in cables)
                        if (cable != null && (cable.Terminal - candidate).sqrMagnitude < .49f) occupied = true;
                if (occupied) continue;
                terminal = candidate;
                return true;
            }
            return false;
        }

        bool Clear(Vector3 a, Vector3 b, bool ignoreStation = false, bool ignoreCommand = false)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / .65f));
            for (int i = 0; i <= steps; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)steps);
                Vector3 local = p - terrain.transform.position;
                TerrainData data = terrain.terrainData;
                float u = local.x / data.size.x, v = local.z / data.size.z;
                if (u < .001f || u > .999f || v < .001f || v > .999f ||
                    data.IsHole((int)(u * data.holesResolution), (int)(v * data.holesResolution))) return false;
                if ((!ignoreStation && Inside(p, origin, stationRadius + .45f)) ||
                    (!ignoreCommand && Inside(p, commandPosition, command.Definition.footprintRadius + .45f))) return false;
                foreach (BuildingInstance building in buildings)
                {
                    if (building == null || building == command || building == consumer) continue;
                    if (building.Kind == BuildingKind.PressureCorridor) continue;
                    if (Inside(p, building.transform.position, building.Definition.footprintRadius + .52f)) return false;
                    Rect ramp = building.Kind == BuildingKind.LandingPad ? LunarLandingPad.RampFootprint :
                        building.Kind == BuildingKind.CargoHangar ? CargoHangarRamp.Footprint : default;
                    if (ramp.width <= 0f) continue;
                    Vector3 relative = building.transform.InverseTransformPoint(p);
                    if (relative.x > ramp.xMin - .45f && relative.x < ramp.xMax + .45f &&
                        relative.z > ramp.yMin - .45f && relative.z < ramp.yMax + .45f) return false;
                }
            }
            return true;
        }

        List<Vector3> Trace(Vector3 start, Vector3 finish)
        {
            // Search in XZ. Elevation is sampled separately when the cable is draped.
            start.y = finish.y = 0f;
            if (Clear(start, finish)) return new List<Vector3> { start, finish };

            var initial = (x: 0, z: 0);
            var costs = new Dictionary<(int x, int z), float> { [initial] = 0f };
            var previous = new Dictionary<(int, int), (int, int)>();
            var open = new SortedSet<(float priority, int order, int x, int z)> { (0f, 0, 0, 0) };
            var closed = new HashSet<(int, int)>();
            int order = 1;
            float maxRadius = Vector3.Distance(start, finish) + 32f;
            while (open.Count > 0 && closed.Count < searchLimit)
            {
                var bestNode = open.Min;
                open.Remove(bestNode);
                var current = (bestNode.x, bestNode.z);
                if (!closed.Add(current)) continue;
                Vector3 here = World(current);
                if (Vector3.Distance(here, finish) <= Pitch * 1.75f && Clear(here, finish))
                {
                    var path = new List<Vector3> { finish, here };
                    while (previous.TryGetValue(current, out var parent))
                    {
                        current = parent;
                        path.Add(World(current));
                    }
                    path.Reverse();
                    var simplified = new List<Vector3> { path[0] };
                    for (int i = 0; i < path.Count - 1;)
                    {
                        int next = path.Count - 1;
                        while (next > i + 1 && !Clear(path[i], path[next])) next--;
                        simplified.Add(path[next]);
                        i = next;
                    }
                    return simplified;
                }
                for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                {
                    var neighbor = (current.x + x, current.z + z);
                    if (closed.Contains(neighbor)) continue;
                    Vector3 point = World(neighbor);
                    if (Vector3.Distance(point, start) > maxRadius) continue;
                    float cost = costs[current] + Vector3.Distance(here, point);
                    if (costs.TryGetValue(neighbor, out float known) && cost >= known || !Clear(here, point)) continue;
                    costs[neighbor] = cost;
                    previous[neighbor] = current;
                    open.Add((cost + Vector3.Distance(point, finish), order++, neighbor.Item1, neighbor.Item2));
                }
            }
            return null;

            Vector3 World((int x, int z) node) => start + new Vector3(node.x * Pitch, 0f, node.z * Pitch);
        }

        Vector3[] Drape(List<Vector3> route)
        {
            var result = new List<Vector3> { route[0] };
            for (int i = 1; i < route.Count; i++)
            {
                int subdivisions = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(route[i - 1], route[i]) / .40f));
                for (int j = 1; j <= subdivisions; j++)
                {
                    Vector3 point = Vector3.Lerp(route[i - 1], route[i], j / (float)subdivisions);
                    float ground = terrain.transform.position.y + terrain.SampleHeight(point) + CableRadius + .037f;
                    if (i == 1 && j < subdivisions && j < 4)
                        ground = Mathf.Max(ground, Mathf.Lerp(route[0].y, ground, j / 4f));
                    if (i == route.Count - 1 && j > subdivisions - 4)
                        ground = Mathf.Max(ground, Mathf.Lerp(ground, route[i].y, (j - subdivisions + 4f) / 4f));
                    if (i == route.Count - 1 && j == subdivisions) ground = route[i].y;
                    point.y = ground;
                    if ((point - result[result.Count - 1]).sqrMagnitude > .0001f) result.Add(point);
                }
            }
            return result.ToArray();
        }
    }

    static bool Inside(Vector3 point, Vector3 center, float radius)
    {
        Vector3 delta = point - center;
        return delta.x * delta.x + delta.z * delta.z < radius * radius;
    }

    internal bool Crosses(Vector3 center, float radius)
    {
        foreach (Vector3 point in points)
            if (Inside(point, center, radius)) return true;
        return false;
    }

    internal static SolarPowerCable Install(BuildingInstance station, BuildingInstance command, Vector3[] route)
    {
        var root = new GameObject("Cable to command post");
        root.transform.SetParent(station.transform, false);
        var connection = root.AddComponent<SolarPowerCable>();
        connection.points = route;
        connection.Command = command;
        var metal = station.GetComponent<MeshRenderer>().sharedMaterials;
        root.GetComponent<MeshRenderer>().sharedMaterial = metal[station.Kind == BuildingKind.LandingPad ? 5 : 2];
        connection.Build(route);
        Connector(route[0], route[1] - route[0]);
        Connector(route[route.Length - 1], route[route.Length - 2] - route[route.Length - 1]);
        return connection;

        void Connector(Vector3 terminal, Vector3 direction)
        {
            var plug = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plug.name = "Shielded power connector";
            plug.transform.SetParent(root.transform, false);
            plug.transform.SetPositionAndRotation(terminal, Quaternion.LookRotation(PressureCorridor.Horizontal(direction)));
            plug.transform.localScale = new Vector3(.44f, .32f, .30f);
            plug.GetComponent<Renderer>().sharedMaterial = metal[1];
            plug.GetComponent<Collider>().enabled = false;
            Destroy(plug.GetComponent<Collider>());
        }
    }

    internal void UpdateCrossings(Terrain terrain, IReadOnlyList<(Vector3 from, Vector3 to)> crossings, IReadOnlyList<(Vector3 center, float radius)> modules)
    {
        if (points == null) return;
        Vector3[] fitted = FitCrossings(terrain, points, crossings, modules);
        if (renderedPoints != null && renderedPoints.Count == fitted.Length)
        {
            bool changed = false;
            for (int i = 0; i < fitted.Length; i++)
                if ((renderedPoints[i] - fitted[i]).sqrMagnitude > .000001f)
                {
                    changed = true;
                    break;
                }
            if (!changed) return;
        }
        Build(fitted);
    }

    internal static Vector3[] FitCrossings(Terrain terrain, IReadOnlyList<Vector3> original,
        IReadOnlyList<(Vector3 from, Vector3 to)> crossings,
        IReadOnlyList<(Vector3 center, float radius)> modules = null, float burialDepth = .23f)
    {
        var route = new Vector3[original.Count];
        for (int i = 0; i < route.Length; i++) route[i] = original[i];
        for (int i = 1; i < route.Length - 1; i++)
        {
            Vector3 point = route[i];
            if (UnderStructure(point))
            {
                float ground = terrain.transform.position.y + terrain.SampleHeight(point);
                point.y = Mathf.Min(point.y, ground - burialDepth);
            }
            route[i] = point;
        }
        const float cableSlope = .42f;
        for (int i = 2; i < route.Length - 1; i++) Lower(i, i - 1);
        for (int i = route.Length - 3; i > 0; i--) Lower(i, i + 1);
        return route;

        bool UnderStructure(Vector3 point)
        {
            foreach (var span in crossings)
                if (PressureCorridor.DistanceToSegment(point, span.from, span.to) <=
                    PressureCorridor.HalfWidth + .30f) return true;
            if (modules != null)
                foreach (var module in modules)
                    if (Inside(point, module.center, module.radius)) return true;
            return false;
        }

        void Lower(int index, int neighbor)
        {
            if (route[neighbor].y >= original[neighbor].y - .0001f) return;
            Vector3 point = route[index];
            Vector3 delta = original[index] - original[neighbor];
            float step = new Vector2(delta.x, delta.z).magnitude;
            point.y = Mathf.Min(point.y, route[neighbor].y + cableSlope * step);
            route[index] = point;
        }
    }

    void Build(Vector3[] route)
    {
        renderedPoints = route;
        const int sides = 8;
        var vertices = new List<Vector3>(route.Length * sides);
        var triangles = new List<int>((route.Length - 1) * sides * 6);
        for (int i = 0; i < route.Length; i++)
        {
            Vector3 axis = PressureCorridor.Horizontal(route[Mathf.Min(i + 1, route.Length - 1)] - route[Mathf.Max(0, i - 1)]);
            Vector3 right = Vector3.Cross(Vector3.up, axis == Vector3.zero ? Vector3.forward : axis);
            for (int side = 0; side < sides; side++)
            {
                float angle = side * Mathf.PI * 2f / sides;
                Vector3 point = route[i] + (right * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle)) * CableRadius;
                vertices.Add(transform.InverseTransformPoint(point));
                if (i == 0) continue;
                int a = (i - 1) * sides + side, b = (i - 1) * sides + (side + 1) % sides;
                triangles.Add(a); triangles.Add(b); triangles.Add(b + sides);
                triangles.Add(a); triangles.Add(b + sides); triangles.Add(a + sides);
            }
        }
        if (mesh == null) mesh = new Mesh { name = "Terrain-conforming electrical cable", indexFormat = IndexFormat.UInt32 };
        else mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    void OnDestroy() => Destroy(mesh);
}
