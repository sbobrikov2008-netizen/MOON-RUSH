using System.Collections.Generic;
using UnityEngine;

public sealed class PressureCorridor : MonoBehaviour
{
    [SerializeField] Material metal;
    [SerializeField] Material glass;
    readonly List<Mesh> meshes = new List<Mesh>();
    Transform geometry;
    readonly List<(Vector3 center, Vector3 size, Quaternion turn)> metalDetails = new List<(Vector3, Vector3, Quaternion)>();
    public BuildingInstance First { get; private set; }
    public BuildingInstance Second { get; private set; }
    public IReadOnlyList<Vector3> Route { get; private set; }

    void Awake()
    {
        Build(new[] { transform.TransformPoint(new Vector3(-9f, 1.8f, 0f)),
            transform.TransformPoint(new Vector3(3f, 1.8f, 0f)),
            transform.TransformPoint(new Vector3(9f, 1.8f, 5f)) }, null);
    }

    public void Connect(BuildingInstance first, BuildingInstance second, Vector3[] route, Terrain terrain)
    {
        First = first;
        Second = second;
        Route = route;
        Build(route, terrain);
    }

    void Build(Vector3[] route, Terrain terrain)
    {
        if (geometry != null)
        {
            geometry.gameObject.SetActive(false);
            Destroy(geometry.gameObject);
            foreach (Mesh mesh in meshes) Destroy(mesh);
            meshes.Clear();
        }
        geometry = new GameObject("Corridor shell and supports").transform;
        geometry.SetParent(transform, false);
        var points = new List<Vector3>();
        var opaque = new List<int>();
        var clear = new List<int>();
        const int sides = 12;
        var path = new List<Vector3> { route[0] };
        for (int i = 1; i < route.Length; i++)
        {
            int divisions = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(route[i - 1], route[i]) / 3f));
            for (int part = 1; part <= divisions; part++)
                path.Add(Vector3.Lerp(route[i - 1], route[i], part / (float)divisions));
            Vector3 center = (route[i] + route[i - 1]) * .5f;
            float length = Vector3.Distance(route[i - 1], route[i]);
            Quaternion facing = Quaternion.LookRotation(route[i] - route[i - 1]);
            Box("Walkable floor", center - Vector3.up * .745f, new Vector3(2.27f, .15f, length), facing, false);
            Box("Roof collider", center + Vector3.up * 1.21f, new Vector3(1.87f, .14f, length), facing, false);
            for (int side = -1; side <= 1; side += 2)
                Box("Side collider", center + (facing * Vector3.right) * (side * 1.22f),
                    new Vector3(.14f, 1.76f, length), facing, false);
        }

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 before = path[i] - path[Mathf.Max(0, i - 1)];
            Vector3 after = path[Mathf.Min(path.Count - 1, i + 1)] - path[i];
            before.y = after.y = 0f;
            if (i == 0) before = after;
            if (i == path.Count - 1) after = before;
            Vector3 left = Vector3.Cross(Vector3.up, before.normalized);
            Vector3 right = Vector3.Cross(Vector3.up, after.normalized);
            Vector3 bisector = (left + right).normalized;
            float mitre = 1f / Mathf.Max(.65f, Vector3.Dot(right, bisector));
            for (int sector = 0; sector < sides; sector++)
            {
                float angle = sector * Mathf.PI * 2f / sides;
                points.Add(transform.InverseTransformPoint(path[i] +
                    bisector * (Mathf.Cos(angle) * 1.33f * mitre) + Vector3.up * (Mathf.Sin(angle) * 1.33f)));
            }

            bool end = i == 0 || i == path.Count - 1;
            // Each short rib is hollow; the walking area inside the tube stays clear.
            float width = end ? .34f : .115f;
            for (int sector = 0; sector < sides; sector++)
            {
                float angle = (sector + .5f) * Mathf.PI * 2f / sides;
                Vector3 radial = bisector * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle);
                Quaternion facing = Quaternion.LookRotation((before.normalized + after.normalized).normalized, radial);
                Box(end ? "Pressure collar" : "Structural rib", path[i] + radial * 1.35f,
                    new Vector3(.74f, .11f, width), facing, true);
            }
            if (terrain != null && !end && i % 2 == 0)
            {
                float bottom = terrain.transform.position.y + terrain.SampleHeight(path[i]);
                float support = path[i].y - 1.18f - bottom;
                if (support > .13f)
                    Box("Anchored pedestal", new Vector3(path[i].x, bottom + support * .5f, path[i].z),
                        new Vector3(.72f, support + .14f, .72f), Quaternion.identity, true);
            }
        }

        for (int span = 0; span < path.Count - 1; span++)
        {
            for (int side = 0; side < sides; side++)
            {
                int a = span * sides + side;
                int b = span * sides + (side + 1) % sides;
                Quad(span % 3 == 1 && (side == 0 || side == 5) ? clear : opaque, a, b, b + sides, a + sides);
            }
            Quad(opaque, span * sides + 7, span * sides + 11, (span + 1) * sides + 11, (span + 1) * sides + 7);
        }

        foreach (var detail in metalDetails)
        {
            int first = points.Count;
            for (int corner = 0; corner < 8; corner++)
            {
                var sign = new Vector3((corner & 1) == 0 ? -.5f : .5f,
                    (corner & 2) == 0 ? -.5f : .5f, (corner & 4) == 0 ? -.5f : .5f);
                points.Add(transform.InverseTransformPoint(detail.center + detail.turn * Vector3.Scale(detail.size, sign)));
            }
            Quad(opaque, first, first + 1, first + 3, first + 2);
            Quad(opaque, first + 4, first + 6, first + 7, first + 5);
            Quad(opaque, first, first + 4, first + 5, first + 1);
            Quad(opaque, first + 2, first + 3, first + 7, first + 6);
            Quad(opaque, first, first + 2, first + 6, first + 4);
            Quad(opaque, first + 1, first + 5, first + 7, first + 3);
        }
        metalDetails.Clear();

        var shell = new GameObject("Pressure skin, windows and deck", typeof(MeshFilter), typeof(MeshRenderer));
        shell.transform.SetParent(geometry, false);
        var skin = new Mesh { name = "Mitered pressure passage" };
        skin.SetVertices(points);
        skin.subMeshCount = 2;
        skin.SetTriangles(opaque, 0);
        skin.SetTriangles(clear, 1);
        skin.RecalculateNormals();
        skin.RecalculateBounds();
        meshes.Add(skin);
        shell.GetComponent<MeshFilter>().sharedMesh = skin;
        shell.GetComponent<MeshRenderer>().sharedMaterials = new[] { metal, glass };
        for (int i = 1; i < path.Count - 1; i += 2)
        {
            var lamp = new GameObject("Interior light", typeof(Light));
            lamp.transform.SetParent(geometry, false);
            lamp.transform.position = path[i] + Vector3.up * .89f;
            Light light = lamp.GetComponent<Light>();
            light.type = LightType.Point;
            light.range = 3.6f;
            light.intensity = 4.2f;
            light.color = new Color(.54f, .82f, 1f);
        }

        static void Quad(List<int> target, int a, int b, int c, int d)
        {
            target.Add(a); target.Add(b); target.Add(c);
            target.Add(a); target.Add(c); target.Add(d);
            target.Add(c); target.Add(b); target.Add(a);
            target.Add(d); target.Add(c); target.Add(a);
        }
    }

    void Box(string label, Vector3 position, Vector3 scale, Quaternion rotation, bool visible)
    {
        if (visible)
        {
            metalDetails.Add((position, scale, rotation));
            return;
        }
        var part = new GameObject(label, typeof(BoxCollider));
        part.transform.SetParent(geometry, false);
        part.transform.SetPositionAndRotation(position, rotation);
        part.transform.localScale = scale;
    }

    public bool Occupies(Vector3 point, float clearance)
    {
        if (Route == null) return false;
        for (int i = 1; i < Route.Count; i++)
            if (DistanceToSegment(point, Route[i - 1], Route[i]) < clearance) return true;
        return false;
    }

    public static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        point.y = a.y = b.y = 0f;
        Vector3 delta = b - a;
        float fraction = Mathf.Clamp01(Vector3.Dot(point - a, delta) / Mathf.Max(delta.sqrMagnitude, .0001f));
        return Vector3.Distance(point, a + delta * fraction);
    }

    void OnDestroy()
    {
        foreach (Mesh mesh in meshes) Destroy(mesh);
    }
}
