using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class PressureCorridor : MonoBehaviour
{
    internal const float HalfWidth = 1.4f;
    internal const float Floor = -1.15f;
    internal const float Roof = 1.48f;
    [SerializeField] Material metal;
    [SerializeField] Material glass;

    readonly List<Vector3> vertices = new List<Vector3>();
    readonly List<int>[] triangles = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
    Transform geometry;
    Mesh mesh;
    Mesh junctionCollision;
    Material hull;
    Material lamp;
    public BuildingInstance First { get; private set; }
    public BuildingInstance Second { get; private set; }
    public IReadOnlyList<Vector3> Route { get; private set; }
    internal PressureCorridor BranchTarget { get; private set; }
    internal Transform StartPort { get; private set; }
    internal Transform EndPort { get; private set; }

    void Awake()
    {
        hull = new Material(metal) { name = "Satin pressure hull" };
        hull.SetColor("_BaseColor", new Color(.33f, .38f, .42f));
        hull.SetFloat("_Smoothness", .22f);
        hull.SetFloat("_Metallic", .25f);
        hull.DisableKeyword("_EMISSION");
        hull.SetColor("_EmissionColor", Color.black);
        lamp = new Material(hull) { name = "Low-output corridor strips" };
        lamp.EnableKeyword("_EMISSION");
        lamp.SetColor("_EmissionColor", new Color(.09f, .22f, .29f));
        BeginGeometry();
        AddSpan(transform.TransformPoint(new Vector3(-9f, 1.8f, 0f)),
            transform.TransformPoint(new Vector3(9f, 1.8f, 0f)), null, true, true);
        FinishGeometry();
    }

    internal void Connect(BuildingInstance first, BuildingInstance second, Vector3[] route,
        Transform start, Transform end, PressureCorridor branchTarget)
    {
        First = first;
        Second = second;
        Route = route;
        StartPort = start;
        EndPort = end;
        BranchTarget = branchTarget;
    }

    internal void BeginGeometry()
    {
        if (geometry != null)
        {
            geometry.gameObject.SetActive(false);
            Destroy(geometry.gameObject);
            Destroy(mesh);
            Destroy(junctionCollision);
            junctionCollision = null;
        }
        geometry = new GameObject("Pressure hull and collision").transform;
        geometry.SetParent(transform, false);
        geometry.gameObject.layer = gameObject.layer;
        vertices.Clear();
        foreach (List<int> indices in triangles) indices.Clear();
    }

    internal void AddSpan(Vector3 from, Vector3 to, Terrain terrain, bool firstCollar, bool lastCollar)
    {
        Vector3 axis = Horizontal(to - from);
        float length = Vector3.Distance(from, to);
        if (length < .04f || axis == Vector3.zero) return;
        Vector3 right = Vector3.Cross(Vector3.up, axis);
        Vector3 center = (from + to) * .5f;
        const float divider = .31f;
        int bays = Mathf.Max(1, Mathf.CeilToInt(length / 3.8f));
        for (int bay = 0; bay < bays; bay++)
        {
            Vector3 a = Vector3.Lerp(from, to, bay / (float)bays);
            Vector3 b = Vector3.Lerp(from, to, (bay + 1f) / bays);
            Surface(a, b, Floor, right, Vector3.up);
            Surface(a, b, Roof, right, Vector3.down);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 offset = right * (side * HalfWidth);
                Vector3 normal = right * side;
                Wall(a + offset, b + offset, Floor, -.06f, normal);
                Wall(a + offset, b + offset, .58f, Roof, normal);
                Vector3 p = Vector3.Lerp(a, b, divider), q = Vector3.Lerp(a, b, 1f - divider);
                Wall(a + offset, p + offset, -.06f, .58f, normal);
                Wall(q + offset, b + offset, -.06f, .58f, normal);
                Wall(p + offset, q + offset, -.06f, .58f, normal, 1);
            }
            Vector3 middle = (a + b) * .5f;
            Box(middle + Vector3.up * (Roof - .038f), new Vector3(.2f, .035f, .48f), axis, 2);
            if (bay != 0) Rib(a, axis, .12f);
            if (terrain != null && (bay % 2 == 0))
            {
                float ground = terrain.transform.position.y + terrain.SampleHeight(middle);
                float rise = middle.y + Floor - ground;
                if (rise > .1f)
                    Box(new Vector3(middle.x, ground + rise * .5f, middle.z),
                        new Vector3(.8f, rise + .12f, .8f), axis, 0);
            }
        }
        Collide(center + Vector3.up * (Floor - .061f), new Vector3(2f * HalfWidth, .12f, length), to - from);
        Collide(center + Vector3.up * (Roof + .061f), new Vector3(2f * HalfWidth, .12f, length), to - from);
        foreach (float side in new[] { -1f, 1f })
            Collide(center + right * side * (HalfWidth + .061f) + Vector3.up * ((Floor + Roof) * .5f),
                new Vector3(.12f, Roof - Floor, length), to - from);
        if (firstCollar) Rib(from + axis * .13f, axis, .26f);
        if (lastCollar) Rib(to - axis * .13f, axis, .26f);
    }

    internal void AddJunction(Vector3 center, List<(Vector3 point, Vector3 direction)> mouths, Terrain terrain)
    {
        var rim = new List<(Vector3 point, int mouth)>();
        for (int i = 0; i < mouths.Count; i++)
        {
            var mouth = mouths[i];
            Vector3 side = Vector3.Cross(Vector3.up, mouth.direction) * HalfWidth;
            rim.Add((mouth.point + side, i));
            rim.Add((mouth.point - side, i));
            Rib(mouth.point, mouth.direction, .18f);
        }
        rim.Sort((a, b) => Angle(a.point).CompareTo(Angle(b.point)));
        for (int i = 0; i < rim.Count; i++)
        {
            var a = rim[i];
            var b = rim[(i + 1) % rim.Count];
            foreach (float level in new[] { Floor, Roof })
            {
                Vector3 height = Vector3.up * level;
                Vector3 normal = level == Floor ? Vector3.up : Vector3.down;
                Face(center + height, a.point + height, b.point + height, b.point + height, normal, 0, true);
                Face(center + height, a.point + height, b.point + height, b.point + height, normal, 3, true);
            }
            if (a.mouth == b.mouth) continue;
            Vector3 outward = Horizontal((a.point + b.point) * .5f - center);
            Wall(a.point, b.point, Floor, Roof, outward, 0);
            Wall(a.point, b.point, Floor, Roof, outward, 3);
        }
        Box(center + Vector3.up * (Roof - .045f), new Vector3(.85f, .045f, .85f), Vector3.forward, 2);

        float ground = terrain.transform.position.y + terrain.SampleHeight(center);
        float rise = center.y + Floor - ground;
        if (rise > .1f)
            Box(new Vector3(center.x, ground + rise * .5f, center.z),
                new Vector3(1.2f, rise + .1f, 1.2f), Vector3.forward, 0);

        float Angle(Vector3 point) => Mathf.Atan2(point.z - center.z, point.x - center.x);
    }

    void Surface(Vector3 a, Vector3 b, float height, Vector3 right, Vector3 normal)
    {
        Vector3 lift = Vector3.up * height;
        Vector3 edge = right * HalfWidth;
        Face(a + lift - edge, a + lift + edge, b + lift + edge, b + lift - edge, normal, 0, twoSided: true);
    }

    void Wall(Vector3 a, Vector3 b, float low, float high, Vector3 normal, int material = 0)
    {
        Face(a + Vector3.up * low, b + Vector3.up * low, b + Vector3.up * high, a + Vector3.up * high,
            normal, material, twoSided: true);
    }

    void Rib(Vector3 point, Vector3 axis, float thickness)
    {
        Vector3 right = Vector3.Cross(Vector3.up, axis);
        foreach (float direction in new[] { -1f, 1f })
            Box(point + right * direction * (HalfWidth + .065f) + Vector3.up * ((Roof + Floor) * .5f),
                new Vector3(.14f, Roof - Floor + .26f, thickness), axis, 0);
        Box(point + Vector3.up * (Roof + .065f), new Vector3(2f * HalfWidth, .14f, thickness), axis, 0);
        Box(point + Vector3.up * (Floor - .065f), new Vector3(2f * HalfWidth, .14f, thickness), axis, 0);
    }

    void Box(Vector3 center, Vector3 size, Vector3 axis, int material)
    {
        Quaternion rotation = Quaternion.LookRotation(axis);
        Vector3 x = rotation * Vector3.right * size.x * .5f;
        Vector3 y = rotation * Vector3.up * size.y * .5f;
        Vector3 z = rotation * Vector3.forward * size.z * .5f;
        Side(x, y, z); Side(y, z, x); Side(z, x, y);
        Side(-x, y, z); Side(-y, z, x); Side(-z, x, y);

        void Side(Vector3 normal, Vector3 u, Vector3 v)
        {
            Vector3 origin = center + normal;
            Face(origin - u - v, origin + u - v, origin + u + v, origin - u + v, normal, material);
        }
    }

    void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, int material, bool twoSided = false)
    {
        // Each side owns its vertices. Sharing them between reversed indices cancels the normals.
        bool reverse = Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f;
        AddTriangle(a, reverse ? c : b, reverse ? b : c);
        if (c != d) AddTriangle(a, reverse ? d : c, reverse ? c : d);
        if (!twoSided) return;
        AddTriangle(a, reverse ? b : c, reverse ? c : b);
        if (c != d) AddTriangle(a, reverse ? c : d, reverse ? d : c);
        void AddTriangle(Vector3 x, Vector3 y, Vector3 z)
        {
            var indices = triangles[material];
            indices.Add(vertices.Count);
            indices.Add(vertices.Count + 1);
            indices.Add(vertices.Count + 2);
            vertices.Add(transform.InverseTransformPoint(x));
            vertices.Add(transform.InverseTransformPoint(y));
            vertices.Add(transform.InverseTransformPoint(z));
        }
    }

    void Collide(Vector3 position, Vector3 size, Vector3 axis)
    {
        var part = new GameObject("Pressure shell collider", typeof(BoxCollider));
        part.transform.SetParent(geometry, false);
        part.layer = gameObject.layer;
        part.transform.SetPositionAndRotation(position, Quaternion.LookRotation(axis));
        part.GetComponent<BoxCollider>().size = size;
    }

    internal void FinishGeometry()
    {
        mesh = new Mesh { name = "Pressure shell", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        mesh.subMeshCount = 3;
        for (int i = 0; i < mesh.subMeshCount; i++) mesh.SetTriangles(triangles[i], i);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var shell = new GameObject("Metal skin and glazing", typeof(MeshFilter), typeof(MeshRenderer));
        shell.transform.SetParent(geometry, false);
        shell.layer = gameObject.layer;
        shell.GetComponent<MeshFilter>().sharedMesh = mesh;
        shell.GetComponent<MeshRenderer>().sharedMaterials = new[] { hull, glass, lamp };
        if (triangles[3].Count > 0)
        {
            // Junction triangles have their own collision surface, with clear openings at each mouth.
            junctionCollision = new Mesh { name = "Junction collision", indexFormat = IndexFormat.UInt32 };
            junctionCollision.SetVertices(vertices);
            junctionCollision.SetTriangles(triangles[3], 0);
            junctionCollision.RecalculateBounds();
            shell.AddComponent<MeshCollider>().sharedMesh = junctionCollision;
        }
    }

    internal static Vector3 Horizontal(Vector3 vector)
    {
        vector.y = 0f;
        return vector.normalized;
    }

    public bool Occupies(Vector3 point, float clearance)
    {
        if (Route == null) return false;
        for (int i = 1; i < Route.Count; i++)
            if (DistanceToSegment(point, Route[i - 1], Route[i]) < clearance) return true;
        return false;
    }

    internal static float Projection(Vector3 point, Vector3 a, Vector3 b)
    {
        point.y = a.y = b.y = 0f;
        Vector3 delta = b - a;
        return Mathf.Clamp01(Vector3.Dot(point - a, delta) / Mathf.Max(delta.sqrMagnitude, .0001f));
    }

    public static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 projected = Vector3.Lerp(a, b, Projection(point, a, b));
        projected.y = point.y;
        return Vector3.Distance(point, projected);
    }

    void OnDestroy()
    {
        Destroy(mesh);
        Destroy(junctionCollision);
        Destroy(hull);
        Destroy(lamp);
    }
}
