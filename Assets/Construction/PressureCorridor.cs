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
    [SerializeField] Texture2D deckTread;

    readonly List<Vector3> vertices = new List<Vector3>();
    const int Hull = 0, Glass = 1, Lamp = 2, Collision = 3, Deck = 4, Markings = 5, Cable = 6;
    readonly List<int>[] triangles = { new List<int>(), new List<int>(), new List<int>(), new List<int>(),
        new List<int>(), new List<int>(), new List<int>() };
    readonly List<Light> fixtures = new List<Light>();
    static readonly Color WarmEmission = new Color(.36f, .19f, .075f);
    Material deck, markings, conduitMaterial;
    bool powered = true;
    float nextFixture;
    Transform geometry;
    Mesh mesh;
    Mesh collisionMesh;
    Material hull;
    Material lamp;
    internal bool VisualOnly { get; set; }
    internal Material PreviewMaterial { get; set; }
    internal MeshRenderer ShellRenderer { get; private set; }
    public BuildingInstance First { get; private set; }
    public BuildingInstance Second { get; private set; }
    public IReadOnlyList<Vector3> Route { get; private set; }
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
        lamp.SetColor("_BaseColor", new Color(.66f, .48f, .29f));
        lamp.SetColor("_EmissionColor", WarmEmission);
        deck = new Material(hull) { name = "Embossed ceramic decking" };
        deck.SetColor("_BaseColor", new Color(.43f, .45f, .47f));
        deck.SetTexture("_BaseMap", deckTread);
        deck.SetFloat("_Metallic", .48f);
        markings = new Material(hull) { name = "Safety stencils and brackets" };
        markings.SetColor("_BaseColor", new Color(.76f, .43f, .14f));
        conduitMaterial = new Material(hull) { name = "Insulated pressure services" };
        conduitMaterial.SetColor("_BaseColor", new Color(.16f, .20f, .23f));
    }

    internal void SetPowered(bool enabled)
    {
        powered = enabled;
        lamp.SetColor("_EmissionColor", enabled ? WarmEmission : Color.black);
        lamp.SetColor("_BaseColor", enabled ? new Color(.66f, .48f, .29f) : new Color(.18f, .15f, .12f));
        foreach (Light fixture in fixtures) fixture.enabled = enabled;
    }

    internal void BuildCatalogSample()
    {
        VisualOnly = true;
        BeginGeometry();
        AddSpan(transform.TransformPoint(new Vector3(-9f, 1.8f, 0f)),
            transform.TransformPoint(new Vector3(9f, 1.8f, 0f)), null, true, true);
        FinishGeometry();
    }

    internal void Connect(BuildingInstance first, BuildingInstance second, Vector3[] route,
        Transform start, Transform end)
    {
        First = first;
        Second = second;
        Route = route;
        StartPort = start;
        EndPort = end;
    }

    internal void BeginGeometry()
    {
        if (geometry != null)
        {
            geometry.gameObject.SetActive(false);
            Destroy(geometry.gameObject);
            Destroy(mesh);
            Destroy(collisionMesh);
            collisionMesh = null;
        }
        geometry = new GameObject("Pressure hull and collision").transform;
        geometry.SetParent(transform, false);
        geometry.gameObject.layer = gameObject.layer;
        fixtures.Clear();
        nextFixture = 1.7f;
        vertices.Clear();
        foreach (List<int> indices in triangles) indices.Clear();
    }

    const float Shoulder = .08f;
    const int ArcSegments = 12;

    internal void AddSpan(Vector3 from, Vector3 to, Terrain terrain, bool firstCollar, bool lastCollar)
    {
        Vector3 axis = Horizontal(to - from);
        float length = Vector3.Distance(from, to);
        if (length < .04f || axis == Vector3.zero) return;
        Vector3 right = Vector3.Cross(Vector3.up, axis);
        int bays = Mathf.Max(1, Mathf.CeilToInt(length / 4.5f));
        float bayLength = length / bays;
        for (int bay = 0; bay < bays; bay++)
        {
            Vector3 a = Vector3.Lerp(from, to, bay / (float)bays);
            Vector3 b = Vector3.Lerp(from, to, (bay + 1f) / bays);
            Vector3 floor = Vector3.up * Floor;
            Vector3 crossBeam = Vector3.Lerp(a, b, .48f) + floor - Vector3.up * .035f;
            Rail(crossBeam - right * 1.24f, crossBeam + right * 1.24f, .065f, .073f, Hull);
            if (Vector3.Distance(a, b) > 1.1f)
            {
                for (int stripe = -1; stripe <= 1; stripe++)
                {
                    Vector3 marker = Vector3.Lerp(a, b, .51f) + floor + right * (stripe * .21f) + Vector3.up * .006f;
                    Box(marker, new Vector3(.105f, .009f, .28f), axis, Markings);
                }
            }
            Vector3 edge = right * HalfWidth;
            Face(a + floor - edge, a + floor + edge, b + floor + edge, b + floor - edge, Vector3.up, Collision, true);
            Vector3 seam = Vector3.up * .007f;
            Vector3 panelA = Vector3.Lerp(a,b,.007f) + floor + seam;
            Vector3 panelB = Vector3.Lerp(a,b,.993f) + floor + seam;
            for (int lane = 0; lane < 3; lane++)
            {
                Vector3 leftEdge = right * (-HalfWidth + lane * (2f * HalfWidth / 3f) + .012f);
                Vector3 rightEdge = right * (-HalfWidth + (lane + 1f) * (2f * HalfWidth / 3f) - .012f);
                Face(panelA + leftEdge, panelA + rightEdge, panelB + rightEdge, panelB + leftEdge,
                    Vector3.up, Deck, true);
            }
            Vector3 inset = Vector3.up * .012f;
            Face(a + floor - edge - inset, a + floor + edge - inset, b + floor + edge - inset,
                b + floor - edge - inset, Vector3.up, Hull, true);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 offset = edge * side;
                Sidewall(a + offset, b + offset, right * side);
                Vector3 light = right * (side * 1.19f) + Vector3.up * (Floor + .027f);
                Rail(a + light, b + light, .052f, .031f, Lamp);
                if (Vector3.Distance(a, b) > 1.1f)
                {
                    Vector3 lane = right * (side * 1.03f) + floor + Vector3.up * .006f;
                    Rail(Vector3.Lerp(a, b, .15f) + lane, Vector3.Lerp(a, b, .77f) + lane, .031f, .008f, Markings);
                }
                Vector3 handrail = right * (side * 1.21f) + Vector3.up * (Floor + .96f);
                Rail(a + handrail, b + handrail, .066f, .063f, 0);
                Vector3 mounting = Vector3.Lerp(a, b, .32f) + handrail;
                Box(mounting + right * (side * .075f) - Vector3.up * .07f,
                    new Vector3(.19f, .20f, .10f), axis, 0);
                Vector3 conduit = right * (side * 1.27f) + Vector3.up * (Floor + .14f);
                Rail(a + conduit, b + conduit, .14f, .17f, Cable);
                Vector3 hardLine = right * (side * 1.31f) + Vector3.up * (Floor + .39f);
                Rail(a + hardLine, b + hardLine, .064f, .063f, Hull);
                Rail(a + hardLine + Vector3.up * .09f, b + hardLine + Vector3.up * .09f, .038f, .041f, Cable);
                Vector3 tie = Vector3.Lerp(a, b, .72f);
                Box(tie + conduit + Vector3.up * .09f, new Vector3(.17f, .23f, .12f), axis, Markings);
                Box(tie + hardLine + Vector3.up * .037f, new Vector3(.12f, .18f, .076f), axis, Hull);
            }
            for (int section = 0; section < ArcSegments; section++)
            {
                Vector3 p = Arch(section, right), q = Arch(section + 1, right);
                Vector3 normal = (p + q) * .5f - Vector3.up * Shoulder;
                Panel(a + p, a + q, b + q, b + p, normal, 1);
            }
            bool illuminate = !VisualOnly && nextFixture <= bayLength;
            if (bayLength > 2.1f || illuminate)
            {
                Vector3 center = Vector3.Lerp(a,b,.52f);
                Vector3 hood = center + Vector3.up * (Roof - .102f);
                float lengthAtCeiling = Mathf.Min(1.52f, bayLength * .74f);
                Box(hood, new Vector3(.52f,.125f,lengthAtCeiling), axis, Hull);
                Box(hood - Vector3.up*.067f, new Vector3(.39f,.022f,lengthAtCeiling*.88f), axis, Lamp);
                if (illuminate)
                {
                    AddFixture(center);
                    nextFixture += 7.4f;
                }
                Rail(a + Vector3.up*(Roof-.18f) + right*.63f, b + Vector3.up*(Roof-.18f) + right*.63f,
                    .17f,.13f,Cable);
            }
            nextFixture -= bayLength;
            if (bay != 0) Rib(a, axis, .095f);
            if (terrain != null && bay % 2 == 0) Support((a + b) * .5f, terrain, .75f);
        }
        if (firstCollar) Dock(from, axis);
        if (lastCollar) Dock(to, -axis);
    }

    void AddFixture(Vector3 at)
    {
        var fixture = new GameObject("Warm deck light", typeof(Light));
        fixture.transform.SetParent(geometry, false);
        fixture.transform.position = at + Vector3.up * (Roof - .23f);
        var light = fixture.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, .72f, .48f);
        light.range = 4.15f;
        light.intensity = 1.15f;
        light.shadows = LightShadows.None;
        light.enabled = powered;
        fixtures.Add(light);
    }

    internal void AddJunction(Vector3 center, List<(Vector3 point, Vector3 direction)> mouths, Terrain terrain)
    {
        var rim = new List<(Vector3 point, int mouth, int arch)>();
        for (int i = 0; i < mouths.Count; i++)
        {
            var mouth = mouths[i];
            Vector3 side = Vector3.Cross(Vector3.up, mouth.direction) * HalfWidth;
            rim.Add((mouth.point + side, i, 0));
            rim.Add((mouth.point - side, i, ArcSegments));
            Rib(mouth.point, mouth.direction, .13f);
        }
        rim.Sort((a, b) => Angle(a.point).CompareTo(Angle(b.point)));
        Vector3 floor = Vector3.up * Floor;
        Vector3 crown = center + Vector3.up * Roof;
        for (int i = 0; i < rim.Count; i++)
        {
            var a = rim[i];
            var b = rim[(i + 1) % rim.Count];
            Panel(center + floor, a.point + floor, b.point + floor, b.point + floor, Vector3.up, Deck);
            if (a.mouth == b.mouth)
            {
                var mouth = mouths[a.mouth];
                Vector3 side = Vector3.Cross(Vector3.up, mouth.direction);
                int step = a.arch < b.arch ? 1 : -1;
                for (int section = a.arch; section != b.arch; section += step)
                {
                    Vector3 p = mouth.point + Arch(section, side);
                    Vector3 q = mouth.point + Arch(section + step, side);
                    Panel(crown, p, q, q, Vector3.up, 1);
                }
                continue;
            }
            Vector3 outward = Horizontal((a.point + b.point) * .5f - center);
            Sidewall(a.point, b.point, outward);
            Vector3 pa = a.point + Vector3.up * Shoulder, pb = b.point + Vector3.up * Shoulder;
            Panel(crown, pa, pb, pb, Vector3.up, 1);
            Rail(pa, pb, .065f, .055f, 0);
            Rail(a.point + floor - outward * .075f, b.point + floor - outward * .075f, .052f, .041f, 2);
        }
        Support(center, terrain, 1.1f);
        float Angle(Vector3 point) => Mathf.Atan2(point.z - center.z, point.x - center.x);
    }

    static Vector3 Arch(int section, Vector3 right)
    {
        float angle = section * Mathf.PI / ArcSegments;
        return right * (HalfWidth * Mathf.Cos(angle)) +
            Vector3.up * (Shoulder + (Roof - Shoulder) * Mathf.Sin(angle));
    }

    void Panel(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, int material)
    {
        Face(a, b, c, d, normal, material, true);
        if (!VisualOnly) Face(a, b, c, d, normal, Collision, true);
    }

    void Sidewall(Vector3 a, Vector3 b, Vector3 outward)
    {
        Panel(a + Vector3.up * Floor, b + Vector3.up * Floor, b + Vector3.up * Shoulder,
            a + Vector3.up * Shoulder, outward, 1);
        Rail(a + Vector3.up * Floor, b + Vector3.up * Floor, .085f, .14f, 0);
    }

    void Support(Vector3 point, Terrain terrain, float width)
    {
        float ground = terrain.transform.position.y + terrain.SampleHeight(point);
        float rise = point.y + Floor - ground;
        if (rise > .08f)
            Box(new Vector3(point.x, ground + rise * .5f, point.z),
                new Vector3(width, rise + .13f, width), Vector3.forward, 0);
    }

    void Rib(Vector3 point, Vector3 axis, float thickness)
    {
        Vector3 right = Vector3.Cross(Vector3.up, axis);
        for (int section = 0; section < ArcSegments; section++)
        {
            Vector3 a = Arch(section, right), b = Arch(section + 1, right);
            RibPiece(point + a, point + b);
        }
        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 edge = right * side * HalfWidth;
            RibPiece(point + edge + Vector3.up * Floor, point + edge + Vector3.up * Shoulder);
        }

        void RibPiece(Vector3 a, Vector3 b)
        {
            Vector3 width = Vector3.Cross(axis, (b - a).normalized) * .046f;
            Vector3 depth = axis * thickness * .5f;
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Face(a + width * sign - depth, b + width * sign - depth, b + width * sign + depth,
                    a + width * sign + depth, width * sign, 0);
                Face(a - width + depth * sign, b - width + depth * sign, b + width + depth * sign,
                    a + width + depth * sign, depth * sign, 0);
            }
        }
    }

    void Dock(Vector3 point, Vector3 axis)
    {
        Rib(point + axis * .08f, axis, .16f);
        Rib(point + axis * .26f, axis, .085f);
        Vector3 right = Vector3.Cross(Vector3.up, axis);
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 location = point + right * (side * 1.27f) + axis * .26f + Vector3.up * (Floor + .27f);
            Box(location, new Vector3(.14f, .32f, .19f), axis, 0);
            Box(location + Vector3.up * .17f, new Vector3(.065f, .041f, .09f), axis, 2);
        }
        for (int section = 0; section < ArcSegments; section++)
        {
            Vector3 a = Arch(section, right), b = Arch(section + 1, right);
            if ((a - b).sqrMagnitude < .0001f) continue;
            Face(point + a, point + b, point + new Vector3(b.x, Roof + .045f, b.z),
                point + new Vector3(a.x, Roof + .045f, a.z), axis, 0, true);
        }
    }

    void Rail(Vector3 a, Vector3 b, float width, float height, int material)
    {
        Vector3 delta = b - a;
        if (delta.sqrMagnitude < .0001f) return;
        Box((a + b) * .5f, new Vector3(width, height, delta.magnitude), delta.normalized, material);
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

    internal void FinishGeometry()
    {
        mesh = new Mesh { name = "Pressure shell", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        var uv = new List<Vector2>(vertices.Count);
        foreach (Vector3 position in vertices) uv.Add(new Vector2(position.x * .71f, position.z * .71f));
        mesh.SetUVs(0, uv);
        int[] visual = { Hull, Glass, Lamp, Deck, Markings, Cable };
        mesh.subMeshCount = visual.Length;
        for (int i = 0; i < visual.Length; i++) mesh.SetTriangles(triangles[visual[i]], i);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var shell = new GameObject("Metal skin and glazing", typeof(MeshFilter), typeof(MeshRenderer));
        shell.transform.SetParent(geometry, false);
        shell.layer = gameObject.layer;
        shell.GetComponent<MeshFilter>().sharedMesh = mesh;
        ShellRenderer = shell.GetComponent<MeshRenderer>();
        ShellRenderer.sharedMaterials = PreviewMaterial == null ? new[] { hull, glass, lamp, deck, markings, conduitMaterial } :
            new[] { PreviewMaterial, PreviewMaterial, PreviewMaterial, PreviewMaterial, PreviewMaterial, PreviewMaterial };
        if (PreviewMaterial != null) ShellRenderer.shadowCastingMode = ShadowCastingMode.Off;
        if (!VisualOnly && triangles[Collision].Count > 0)
        {
            // The collision surface follows the curved shell and leaves the mouths open.
            collisionMesh = new Mesh { name = "Pressure shell collision", indexFormat = IndexFormat.UInt32 };
            collisionMesh.SetVertices(vertices);
            collisionMesh.SetTriangles(triangles[Collision], 0);
            collisionMesh.RecalculateBounds();
            shell.AddComponent<MeshCollider>().sharedMesh = collisionMesh;
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
        Destroy(collisionMesh);
        Destroy(hull);
        Destroy(lamp);
        Destroy(deck);
        Destroy(markings);
        Destroy(conduitMaterial);
    }
}
