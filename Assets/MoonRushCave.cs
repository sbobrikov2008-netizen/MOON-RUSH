using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Terrain), typeof(TerrainCollider))]
public sealed class MoonRushCave : MonoBehaviour
{
    [SerializeField] Material rockMaterial;

    const float Length = 33f;
    const float HalfWidth = 7f;
    const float RoofHeight = 7.5f;
    const int Sides = 10;
    const int Rings = 7;

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

        FindSite(out Vector3 mouth, out Vector3 inward);
        Vector3 across = Vector3.Cross(Vector3.up, inward);
        mesh = BuildMesh(mouth, inward, across);
        instance = Instantiate(original);
        CutEntrance(mouth, inward, across);
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

    void FindSite(out Vector3 mouth, out Vector3 inward)
    {
        Vector3 center = original.size * .5f;
        center.y = 0f;
        float size = Mathf.Min(original.size.x, original.size.z);
        float best = float.NegativeInfinity;
        mouth = center;
        inward = Vector3.forward;
        // Look across the uphill faces visible in front of the initial camera.
        for (int angle = -22; angle <= 78; angle += 5)
        {
            Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            for (float radius = .205f; radius <= .39f; radius += .008f)
            {
                Vector3 site = center + direction * (radius * size);
                Vector3 across = Vector3.Cross(Vector3.up, direction);
                float floor = HeightAt(site - direction * 4f);
                float back = HeightAt(site + direction * Length);
                float front = HeightAt(site - direction * 13f);
                float shoulder = Mathf.Abs(HeightAt(site + across * 8f) - HeightAt(site - across * 8f));
                float score = -Mathf.Abs(back - floor - 14f) * 1.1f - Mathf.Abs(floor - front - 2f)
                    - shoulder - Mathf.Abs(angle - 30) * .06f - (radius - .205f) * 38f;
                if (score <= best) continue;
                best = score;
                mouth = new Vector3(site.x, floor - .25f, site.z);
                inward = direction;
            }
        }
    }

    float HeightAt(Vector3 local)
    {
        return original.GetInterpolatedHeight(local.x / original.size.x, local.z / original.size.z);
    }

    void CutEntrance(Vector3 mouth, Vector3 inward, Vector3 across)
    {
        int resolution = instance.holesResolution;
        Vector2 spacing = new Vector2(instance.size.x, instance.size.z) / resolution;
        Vector3 end = mouth + inward * Length;
        const float buffer = 12f;
        int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(mouth.x, end.x) - buffer) / spacing.x));
        int z0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(mouth.z, end.z) - buffer) / spacing.y));
        int x1 = Mathf.Min(resolution, Mathf.CeilToInt((Mathf.Max(mouth.x, end.x) + buffer) / spacing.x));
        int z1 = Mathf.Min(resolution, Mathf.CeilToInt((Mathf.Max(mouth.z, end.z) + buffer) / spacing.y));
        bool[,] holes = instance.GetHoles(x0, z0, x1 - x0, z1 - z0);
        for (int z = 0; z < z1 - z0; z++)
        for (int x = 0; x < x1 - x0; x++)
        {
            var offset = new Vector3((x0 + x + .5f) * spacing.x - mouth.x, 0,
                (z0 + z + .5f) * spacing.y - mouth.z);
            float depth = Vector3.Dot(offset, inward);
            if (depth < .5f || depth > Length - 1f ||
                Mathf.Abs(Vector3.Dot(offset, across)) >= HalfWidth - 1f) continue;

            // Keep existing terrain above the tunnel where the rock already covers it.
            float height = HeightAt(mouth + offset) - mouth.y;
            if (height >= -.4f && height <= RoofHeight + 1.5f) holes[z, x] = false;
        }
        instance.SetHoles(x0, z0, holes);
    }

    Mesh BuildMesh(Vector3 mouth, Vector3 inward, Vector3 across)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int shell = 0; shell < 2; shell++)
        for (int ring = 0; ring < Rings; ring++)
        for (int side = 0; side < Sides; side++)
        {
            float angle = (side - 2) * Mathf.PI * 2f / Sides;
            float depth = Length * ring / (Rings - 1);
            float noise = Mathf.PerlinNoise(side * .71f + 4f, ring * .63f + 8f) - .5f;
            float width = HalfWidth * (1f - .12f * ring / (Rings - 1f));
            float x = Mathf.Cos(angle) * width * (1f + noise * .19f);
            float y = Mathf.Max(0f, Mathf.Sin(angle)) * RoofHeight * (1f + noise * .25f);
            if (shell == 1)
            {
                x *= 1.72f;
                float rockHeight = HeightAt(mouth + across * x + inward * depth) - mouth.y;
                float cover = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((y - 1f) / 3f));
                y = Mathf.Lerp(rockHeight - 2f, Mathf.Max(y + 1.6f + noise * 2f, rockHeight - .35f), cover);
                if (ring == 0) depth = -1.1f + noise * 2.8f;
            }
            vertices.Add(new Vector3(x, y, depth));
        }

        int outer = Rings * Sides;
        for (int ring = 0; ring < Rings - 1; ring++)
        for (int side = 0; side < Sides; side++)
        {
            int a = ring * Sides + side;
            int b = ring * Sides + (side + 1) % Sides;
            AddQuad(triangles, a, a + Sides, b + Sides, b);
            AddQuad(triangles, outer + a, outer + b, outer + b + Sides, outer + a + Sides);
            if (ring == 0) AddQuad(triangles, a, b, outer + b, outer + a);
        }

        int end = (Rings - 1) * Sides;
        int cap = vertices.Count;
        vertices.Add(new Vector3(0, 2.8f, Length + 1f));
        for (int side = 0; side < Sides; side++)
        {
            triangles.Add(end + (side + 1) % Sides);
            triangles.Add(end + side);
            triangles.Add(cap);
        }

        for (int index = 0; index < 7; index++)
        {
            float x = (index % 2 == 0 ? -1f : 1f) * (8.3f + (index % 3) * 1.3f);
            float z = -2f - (index * 7 % 11);
            float y = HeightAt(mouth + across * x + inward * z) - mouth.y;
            AddBoulder(vertices, triangles, new Vector3(x, y - .2f, z), .55f + (index % 4) * .32f);
        }

        var result = new Mesh { name = "Lunar cave shell, floor and boulders" };
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
