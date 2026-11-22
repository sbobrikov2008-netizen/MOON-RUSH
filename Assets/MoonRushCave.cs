using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Terrain), typeof(TerrainCollider))]
public sealed class MoonRushCave : MonoBehaviour
{
    [SerializeField] Material rockMaterial;
    [SerializeField] MoonRushCamera strategyCamera;

    const float HalfWidth = 18f;
    const float Crown = 23f;
    const float Length = 49f;
    const float PortalRun = 14f;
    const int Segments = 64;
    const int TunnelRings = 11;

    Terrain terrain;
    TerrainCollider terrainCollider;
    TerrainData original;
    TerrainData instance;
    Mesh mesh;
    Material entranceMaterial;
    Material interiorMaterial;

    void Start()
    {
        terrain = GetComponent<Terrain>();
        terrainCollider = GetComponent<TerrainCollider>();
        original = terrain.terrainData;
        if (original == null) return;
        instance = Instantiate(original);
        RepairWestSlope();
        terrain.terrainData = instance;
        terrainCollider.terrainData = instance;

        if (rockMaterial == null || rockMaterial.shader == null ||
            rockMaterial.shader.name != "Universal Render Pipeline/Lit" || !rockMaterial.shader.isSupported)
            return;

        if (!FindSite(out Vector3 site))
        {
            Debug.LogWarning("MOON RUSH: No mountainside with enough cover for the cavern.", this);
            return;
        }

        mesh = BuildPatch(site);
        terrain.Flush();

        entranceMaterial = new Material(rockMaterial) { name = "Dusty lunar rock" };
        TerrainLayer[] layers = original.terrainLayers;
        if (layers.Length > 0 && layers[0] != null && layers[0].diffuseTexture != null)
        {
            entranceMaterial.SetTexture("_BaseMap", layers[0].diffuseTexture);
            entranceMaterial.SetColor("_BaseColor", new Color(.77f, .77f, .77f));
        }
        interiorMaterial = new Material(entranceMaterial) { name = "Deep lunar basalt" };
        Color shade = entranceMaterial.GetColor("_BaseColor");
        interiorMaterial.SetColor("_BaseColor", new Color(shade.r * .68f, shade.g * .68f, shade.b * .68f));

        var cave = new GameObject("Lunar cave", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        cave.transform.SetParent(transform, false);
        cave.transform.localPosition = site;
        cave.GetComponent<MeshFilter>().sharedMesh = mesh;
        cave.GetComponent<MeshRenderer>().sharedMaterials = new[] { entranceMaterial, interiorMaterial };
        cave.GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    void OnDestroy()
    {
        if (instance == null) return;
        if (terrain != null && terrain.terrainData == instance) terrain.terrainData = original;
        if (terrainCollider != null && terrainCollider.terrainData == instance) terrainCollider.terrainData = original;
        Destroy(instance);
        Destroy(mesh);
        Destroy(entranceMaterial);
        Destroy(interiorMaterial);
    }

    void RepairWestSlope()
    {
        // The shipped 900 m valley has a discontinuity at the join between rows
        // 255 and 256. Fresh landscapes have no such join and need no repair.
        if (instance.heightmapResolution != 513 || Mathf.Abs(instance.size.x - 900f) > .1f)
            return;
        const int firstRow = 228;
        const int lastRow = 283;
        const int samples = 120;
        const int join = 255 - firstRow + 1;
        float[,] patch = instance.GetHeights(0, firstRow - 1, samples, lastRow - firstRow + 3);
        float verticalScale = instance.size.y;
        float peak = 0f;
        for (int x = 0; x < samples; x++)
            peak = Mathf.Max(peak, Mathf.Abs(patch[join + 1, x] - patch[join, x]) * verticalScale);
        if (peak < 18f) return;

        const int rows = lastRow - firstRow + 1;
        var result = new float[rows, samples];
        for (int x = 0; x < samples; x++)
        {
            float a = patch[1, x];
            float b = patch[rows, x];
            float run = rows - 1;
            float maxTangent = Mathf.Abs(b - a) * 1.5f + .025f;
            float slopeA = Mathf.Clamp((a - patch[0, x]) * run, -maxTangent, maxTangent);
            float slopeB = Mathf.Clamp((patch[rows + 1, x] - b) * run, -maxTangent, maxTangent);
            float influence = Mathf.SmoothStep(0f, 1f,
                Mathf.Abs(patch[join + 1, x] - patch[join, x]) * verticalScale / 8f);
            for (int row = 0; row < rows; row++)
            {
                float t = row / run;
                float u = 1f - t;
                float bridge = u * u * (1f + 2f * t) * a + t * t * (3f - 2f * t) * b
                    + t * u * u * slopeA - t * t * u * slopeB;
                result[row, x] = Mathf.Clamp01(Mathf.Lerp(patch[row + 1, x], bridge, influence));
            }
        }
        instance.SetHeights(0, firstRow, result);
    }

    bool FindSite(out Vector3 site)
    {
        Vector3 size = instance.size;
        var camera = strategyCamera != null ? strategyCamera.GetComponent<Camera>() : null;
        float best = float.NegativeInfinity;
        site = default;
        for (float z = size.z * .5f + 185f; z < Mathf.Min(size.z - 90f, size.z * .5f + 330f); z += 3f)
        for (float x = size.x * .5f - 70f; x < size.x * .5f + 235f; x += 4f)
        {
            float floor = HeightAt(x, z - PortalRun);
            float rise = HeightAt(x, z + PortalRun) - floor;
            if (rise < 18f) continue;
            float cover = float.PositiveInfinity;
            for (int side = -1; side <= 1; side++)
            for (int depth = 21; depth <= 45; depth += 8)
                cover = Mathf.Min(cover, HeightAt(x + side * 11f, z + depth) - floor);
            if (cover < 19f) continue;

            float score = -Mathf.Abs(rise - Crown) - Mathf.Abs(x - size.x * .60f) * .035f
                - Mathf.Abs(z - size.z * .5f - 260f) * .034f;
            if (camera != null)
            {
                Vector3 view = camera.WorldToViewportPoint(transform.TransformPoint(new Vector3(x, floor + 10f, z)));
                score -= Mathf.Abs(view.x - .5f) * 4f;
            }
            if (score <= best) continue;
            best = score;
            site = new Vector3(x, floor, z);
        }
        return !float.IsNegativeInfinity(best);
    }

    float HeightAt(float x, float z) => instance.GetInterpolatedHeight(x / instance.size.x, z / instance.size.z);

    Mesh BuildPatch(Vector3 site)
    {
        int resolution = original.holesResolution;
        float dx = original.size.x / resolution;
        float dz = original.size.z / resolution;
        int x0 = Mathf.FloorToInt((site.x - 51f) / dx);
        int x1 = Mathf.CeilToInt((site.x + 51f) / dx);
        int z0 = Mathf.FloorToInt((site.z - 65f) / dz);
        int z1 = Mathf.CeilToInt((site.z + 38f) / dz);
        int width = x1 - x0;
        int height = z1 - z0;
        float scaleY = original.size.y;
        float[,] heights = instance.GetHeights(x0, z0, width + 1, height + 1);
        for (int z = 0; z <= height; z++)
        for (int x = 0; x <= width; x++)
        {
            float px = (x0 + x) * dx - site.x;
            float pz = (z0 + z) * dz - site.z;
            float across = 1f - Mathf.SmoothStep(0f, 1f, (Mathf.Abs(px) - 13f) / 15f);
            float approach = Mathf.SmoothStep(0f, 1f, (pz + 47f) / 22f);
            float rear = 1f - Mathf.SmoothStep(0f, 1f, (pz + 11f) / 10f);
            float floor = (site.y - 1.3f) / scaleY;
            heights[z, x] = Mathf.Lerp(heights[z, x], Mathf.Min(heights[z, x], floor),
                across * approach * rear);
        }
        instance.SetHeights(x0, z0, heights);

        bool[,] holes = instance.GetHoles(x0, z0, width, height);
        var cut = new bool[height, width];
        for (int z = 1; z < height - 1; z++)
        for (int x = 1; x < width - 1; x++)
        {
            float px = ((x0 + x + .5f) * dx - site.x) / HalfWidth;
            float pz = ((z0 + z + .5f) * dz - site.z) / PortalRun;
            float theta = Mathf.Atan2(pz, px);
            float outline = .94f + .055f * Mathf.Sin(theta * 3f + .9f) + .035f * Mathf.Sin(theta * 7f);
            if (px * px + pz * pz >= outline * outline || !holes[z, x]) continue;
            cut[z, x] = true;
            holes[z, x] = false;
        }

        var points = new List<Vector3>();
        var rock = new List<int>();
        var dark = new List<int>();
        for (int ring = 0; ring <= TunnelRings; ring++)
        for (int i = 0; i < Segments; i++)
        {
            float t = ring / (float)TunnelRings;
            Vector3 lip = Lip(i * Mathf.PI * 2f / Segments);
            float noise = Mathf.PerlinNoise(lip.x * .13f + ring * .51f + 16f, lip.y * .19f + 5f) - .5f;
            float chamber = 1f + Mathf.Sin(t * Mathf.PI) * .23f - .52f * t * t;
            float x = lip.x * chamber + Mathf.Sin(t * 3.1f) * 7f;
            float z = Mathf.Lerp(lip.z, Length, t) + noise * t * 2.8f;
            float floor = -1.3f - t * 3f;
            float y = floor + (lip.y + 1.3f) * (1f - .59f * t + noise * t * .31f);
            if (ring > 0)
            {
                float rockCeiling = HeightAt(site.x + x, site.z + z) - site.y - 1.8f;
                float buried = Mathf.SmoothStep(0f, 1f, (z - PortalRun * .72f) / 11f);
                y = Mathf.Lerp(y, Mathf.Min(y, Mathf.Max(floor + .2f, rockCeiling)), buried);
            }
            points.Add(new Vector3(x, y, z));
        }
        for (int ring = 0; ring < TunnelRings; ring++)
        for (int i = 0; i < Segments; i++)
        {
            int a = ring * Segments + i;
            int b = ring * Segments + (i + 1) % Segments;
            AddQuad(ring <= 1 ? rock : dark, a, a + Segments, b + Segments, b);
        }

        int cap = points.Count;
        int end = TunnelRings * Segments;
        points.Add(new Vector3(2f, 5f, Length + 2f));
        for (int i = 0; i < Segments; i++)
        {
            dark.Add(end + (i + 1) % Segments);
            dark.Add(end + i);
            dark.Add(cap);
        }

        for (int z = 1; z < height - 1; z++)
        for (int x = 1; x < width - 1; x++)
        {
            if (!cut[z, x]) continue;
            if (holes[z - 1, x]) Stitch(x, z, x + 1, z);
            if (holes[z, x + 1]) Stitch(x + 1, z, x + 1, z + 1);
            if (holes[z + 1, x]) Stitch(x + 1, z + 1, x, z + 1);
            if (holes[z, x - 1]) Stitch(x, z + 1, x, z);
        }

        var result = new Mesh { name = "Lunar cave cutaway and dark passage" };
        result.SetVertices(points);
        var uv = new List<Vector2>(points.Count);
        foreach (Vector3 point in points)
            uv.Add(new Vector2((site.x + point.x) / 32f, (site.z + point.z + point.y * .65f) / 32f));
        result.SetUVs(0, uv);
        result.subMeshCount = 2;
        result.SetTriangles(rock, 0);
        result.SetTriangles(dark, 1);
        result.RecalculateNormals();
        result.RecalculateBounds();
        instance.SetHoles(x0, z0, holes);
        return result;

        Vector3 Corner(int x, int z) => new Vector3((x0 + x) * dx - site.x,
            heights[z, x] * scaleY - site.y, (z0 + z) * dz - site.z);

        void Stitch(int ax, int az, int bx, int bz)
        {
            int start = points.Count;
            Vector3 a = Corner(ax, az);
            Vector3 b = Corner(bx, bz);
            points.Add(a);
            points.Add(b);
            points.Add(NearestLip(b, points));
            points.Add(NearestLip(a, points));
            AddQuad(rock, start, start + 3, start + 2, start + 1);
        }
    }

    static Vector3 Lip(float angle)
    {
        float x = Mathf.Cos(angle);
        float z = Mathf.Sin(angle);
        float noise = Mathf.PerlinNoise(x * 3.9f + 11f, z * 4.1f + 20f) - .5f;
        float ledge = Mathf.Sin(angle * 5f + .7f) * .075f + Mathf.Sin(angle * 11f) * .03f;
        return new Vector3(x * HalfWidth * (1f + noise * .15f + ledge) + 2.5f * z,
            Mathf.Max(0f, z) * Crown * (1f + noise * .25f + ledge) - 1.3f,
            z * PortalRun * (1f + noise * .15f) + x * 2.4f);
    }

    static Vector3 NearestLip(Vector3 point, List<Vector3> vertices)
    {
        float angle = Mathf.Atan2(point.z / PortalRun, point.x / HalfWidth);
        float segment = Mathf.Repeat(angle / (Mathf.PI * 2f), 1f) * Segments;
        int first = Mathf.FloorToInt(segment) % Segments;
        return Vector3.Lerp(vertices[first], vertices[(first + 1) % Segments], segment - Mathf.Floor(segment));
    }

    static void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a); triangles.Add(b); triangles.Add(c);
        triangles.Add(a); triangles.Add(c); triangles.Add(d);
    }
}
