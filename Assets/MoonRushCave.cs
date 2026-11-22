using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Terrain), typeof(TerrainCollider))]
public sealed class MoonRushCave : MonoBehaviour
{
    [SerializeField] Material rockMaterial;

    const float Length = 42f;
    const float PortalDepth = 28f;
    const float HalfWidth = 12f;
    const float RoofHeight = 11f;
    const int Sides = 14;
    const int Rings = 13;

    Terrain terrain;
    TerrainCollider terrainCollider;
    TerrainData original;
    TerrainData instance;
    Mesh mesh;

    void Awake()
    {
        terrain = GetComponent<Terrain>();
        terrainCollider = GetComponent<TerrainCollider>();
        original = terrain.terrainData;
        if (original == null || rockMaterial == null || rockMaterial.shader == null ||
            rockMaterial.shader.name != "Universal Render Pipeline/Lit" ||
            !rockMaterial.shader.isSupported) return;

        if (!FindSite(out Vector3 mouth, out Vector3 inward))
        {
            Debug.LogWarning("MOON RUSH: No hillside has enough cover for the cave. Apply Lunar Landscape and try again.", this);
            return;
        }
        Vector3 across = Vector3.Cross(Vector3.up, inward);
        instance = Instantiate(original);
        mesh = BuildMesh(mouth, inward, across);
        terrain.terrainData = instance;
        terrainCollider.terrainData = instance;

        var cave = new GameObject("Lunar cave", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        cave.transform.SetParent(transform, false);
        cave.transform.localPosition = mouth;
        cave.transform.rotation = Quaternion.LookRotation(inward);
        cave.GetComponent<MeshFilter>().sharedMesh = mesh;
        cave.GetComponent<MeshRenderer>().sharedMaterial = rockMaterial;
        cave.GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    void OnDestroy()
    {
        if (instance == null) return;
        if (terrain != null && terrain.terrainData == instance) terrain.terrainData = original;
        if (terrainCollider != null && terrainCollider.terrainData == instance) terrainCollider.terrainData = original;
        Destroy(instance);
        Destroy(mesh);
    }

    bool FindSite(out Vector3 mouth, out Vector3 inward)
    {
        Vector3 center = original.size * .5f;
        center.y = 0f;
        float size = Mathf.Min(original.size.x, original.size.z);
        float best = float.NegativeInfinity;
        mouth = center;
        inward = Vector3.forward;
        for (int angle = -125; angle <= 230; angle += 5)
        {
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 across = Vector3.Cross(Vector3.up, direction);
            for (float radius = .19f; radius <= .39f; radius += .004f)
            {
                Vector3 site = center + direction * (radius * size);
                float floor = HeightAt(site) + .4f;
                float cover = float.PositiveInfinity;
                for (float depth = PortalDepth; depth <= Length; depth += 3.5f)
                for (int shoulder = -1; shoulder <= 1; shoulder++)
                {
                    float height = HeightAt(site + direction * depth + across * (shoulder * 7f));
                    cover = Mathf.Min(cover, height - floor);
                }
                if (cover < RoofHeight + 1f) continue;

                float rise = HeightAt(site + direction * 10f) - floor;
                float approach = floor - HeightAt(site - direction * 8f);
                if (rise < 1f || rise > RoofHeight || approach < -.5f || approach > 4.5f) continue;

                float score = -Mathf.Abs(rise - 5f) - Mathf.Abs(approach - 2f)
                    - Mathf.Abs(Mathf.DeltaAngle(angle, 35f)) * .055f - radius * 24f;
                if (score <= best) continue;
                best = score;
                mouth = new Vector3(site.x, floor, site.z);
                inward = direction;
            }
        }
        return best > float.NegativeInfinity;
    }

    float HeightAt(Vector3 local)
    {
        return original.GetInterpolatedHeight(local.x / original.size.x, local.z / original.size.z);
    }

    void CutEntrance(Vector3 mouth, Vector3 inward, Vector3 across,
        List<Vector3> vertices, List<int> triangles)
    {
        int resolution = instance.holesResolution;
        Vector2 spacing = new Vector2(instance.size.x, instance.size.z) / resolution;
        Vector3 end = mouth + inward * PortalDepth;
        const float buffer = HalfWidth + 4f;
        int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(mouth.x, end.x) - buffer) / spacing.x));
        int z0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(mouth.z, end.z) - buffer) / spacing.y));
        int width = Mathf.Min(resolution, Mathf.CeilToInt((Mathf.Max(mouth.x, end.x) + buffer) / spacing.x)) - x0;
        int height = Mathf.Min(resolution, Mathf.CeilToInt((Mathf.Max(mouth.z, end.z) + buffer) / spacing.y)) - z0;
        bool[,] holes = instance.GetHoles(x0, z0, width, height);
        var cut = new bool[height, width];
        for (int z = 1; z < height - 1; z++)
        for (int x = 1; x < width - 1; x++)
        {
            var sample = new Vector3((x0 + x + .5f) * spacing.x, 0f, (z0 + z + .5f) * spacing.y);
            Vector3 local = ToCave(sample, mouth, inward, across);
            if (local.z < 0f || local.z >= PortalDepth || Mathf.Abs(local.x) > HalfWidth) continue;
            float crown = RoofHeight * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(local.x / HalfWidth, 2)));
            if (local.y < -.25f || local.y > crown + .65f || !holes[z, x]) continue;

            holes[z, x] = false;
            cut[z, x] = true;
        }

        // Join each exposed heightfield edge to the tunnel wall. The lip follows the
        // original slope, so no freestanding shell or open vertical terrain cut remains.
        for (int z = 1; z < height - 1; z++)
        for (int x = 1; x < width - 1; x++)
        {
            if (!cut[z, x]) continue;
            if (holes[z - 1, x]) Stitch(x, z, x + 1, z);
            if (holes[z, x + 1]) Stitch(x + 1, z, x + 1, z + 1);
            if (holes[z + 1, x]) Stitch(x + 1, z + 1, x, z + 1);
            if (holes[z, x - 1]) Stitch(x, z + 1, x, z);
        }
        instance.SetHoles(x0, z0, holes);

        void Stitch(int ax, int az, int bx, int bz)
        {
            Vector3 a = Corner(ax, az);
            Vector3 b = Corner(bx, bz);
            Vector3 innerA = ClosestWall(a, vertices);
            Vector3 innerB = ClosestWall(b, vertices);
            Vector3 edge = (a + b) * .5f;
            Vector3 facing = new Vector3(-edge.x, 3f - edge.y, -3f);
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(innerB);
            vertices.Add(innerA);
            if (Vector3.Dot(Vector3.Cross(b - a, innerB - a), facing) >= 0f)
                AddQuad(triangles, start, start + 1, start + 2, start + 3);
            else
                AddQuad(triangles, start + 3, start + 2, start + 1, start);
        }

        Vector3 Corner(int x, int z) => ToCave(
            new Vector3((x0 + x) * spacing.x, 0f, (z0 + z) * spacing.y), mouth, inward, across);
    }

    Vector3 ToCave(Vector3 point, Vector3 mouth, Vector3 inward, Vector3 across)
    {
        Vector3 delta = point - mouth;
        return new Vector3(Vector3.Dot(delta, across), HeightAt(point) - mouth.y, Vector3.Dot(delta, inward));
    }

    static Vector3 ClosestWall(Vector3 point, List<Vector3> vertices)
    {
        float ring = Mathf.Clamp01(point.z / Length) * (Rings - 1);
        int lower = Mathf.Min(Rings - 2, Mathf.FloorToInt(ring));
        float fraction = ring - lower;
        var query = new Vector2(point.x, point.y);
        Vector3 closest = Vector3.zero;
        float best = float.PositiveInfinity;
        for (int side = 0; side < Sides; side++)
        {
            int next = (side + 1) % Sides;
            Vector3 a = Vector3.Lerp(vertices[lower * Sides + side], vertices[(lower + 1) * Sides + side], fraction);
            Vector3 b = Vector3.Lerp(vertices[lower * Sides + next], vertices[(lower + 1) * Sides + next], fraction);
            Vector2 edge = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(query - (Vector2)a, edge) / Mathf.Max(.001f, edge.sqrMagnitude));
            Vector3 hit = Vector3.Lerp(a, b, t);
            float distance = (query - (Vector2)hit).sqrMagnitude;
            if (distance >= best) continue;
            best = distance;
            closest = hit;
        }
        return closest;
    }

    Mesh BuildMesh(Vector3 mouth, Vector3 inward, Vector3 across)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int ring = 0; ring < Rings; ring++)
        for (int side = 0; side < Sides; side++)
        {
            float t = ring / (Rings - 1f);
            float angle = side * Mathf.PI / (Sides - 1);
            float noise = Mathf.PerlinNoise(side * .73f + 4f, ring * .61f + 8f) - .5f;
            float width = HalfWidth * (1f - .26f * t + noise * .13f);
            float x = Mathf.Cos(angle) * width + 1.6f * t * Mathf.Sin(t * 4f);
            float y = Mathf.Sin(angle) * RoofHeight * (1f - .18f * t + noise * .18f);
            vertices.Add(new Vector3(x, y - t * 1.2f, t * Length));
        }

        for (int ring = 0; ring < Rings - 1; ring++)
        for (int side = 0; side < Sides; side++)
        {
            int a = ring * Sides + side;
            int b = ring * Sides + (side + 1) % Sides;
            AddQuad(triangles, a, a + Sides, b + Sides, b);
        }

        int end = (Rings - 1) * Sides;
        int cap = vertices.Count;
        vertices.Add(new Vector3(0f, 2.8f, Length + 1f));
        for (int side = 0; side < Sides; side++)
        {
            triangles.Add(end + (side + 1) % Sides);
            triangles.Add(end + side);
            triangles.Add(cap);
        }

        CutEntrance(mouth, inward, across, vertices, triangles);
        for (int index = 0; index < 7; index++)
        {
            float x = (index % 2 == 0 ? -1f : 1f) * (12.5f + (index % 3) * 1.4f);
            float z = 4f - (index * 7 % 11);
            float y = HeightAt(mouth + across * x + inward * z) - mouth.y;
            AddBoulder(vertices, triangles, new Vector3(x, y - .2f, z), .6f + (index % 4) * .45f);
        }

        var result = new Mesh { name = "Embedded lunar cave" };
        result.SetVertices(vertices);
        result.SetTriangles(triangles, 0);
        result.RecalculateNormals();
        result.RecalculateBounds();
        return result;
    }

    static void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a); triangles.Add(b); triangles.Add(c);
        triangles.Add(a); triangles.Add(c); triangles.Add(d);
    }

    static void AddBoulder(List<Vector3> vertices, List<int> triangles, Vector3 center, float size)
    {
        int start = vertices.Count;
        vertices.Add(center + new Vector3(.12f, .89f, -.2f) * size);
        vertices.Add(center + Vector3.down * size * .36f);
        for (int side = 0; side < 5; side++)
        {
            float angle = side * Mathf.PI * .4f;
            vertices.Add(center + new Vector3(Mathf.Cos(angle), .19f * (side % 2),
                Mathf.Sin(angle) * .76f) * size);
        }
        for (int side = 0; side < 5; side++)
        {
            int a = start + 2 + side;
            int b = start + 2 + (side + 1) % 5;
            triangles.Add(start); triangles.Add(b); triangles.Add(a);
            triangles.Add(start + 1); triangles.Add(a); triangles.Add(b);
        }
    }
}
