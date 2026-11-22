using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Terrain), typeof(TerrainCollider))]
public sealed class MoonRushCave : MonoBehaviour
{
    [SerializeField] Material rockMaterial;
    [SerializeField] MoonRushCamera strategyCamera;

    const float HalfWidth = 28f;
    const float Crown = 24f;
    const float Length = 67f;
    const float PortalRun = 20f;
    const float SlopeSample = 12f;
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
        if (original == null || rockMaterial == null || rockMaterial.shader == null ||
            rockMaterial.shader.name != "Universal Render Pipeline/Lit" || !rockMaterial.shader.isSupported)
            return;

        if (!FindSite(out Vector3 site))
        {
            Debug.LogWarning("MOON RUSH: No suitable mountain slope in the starting camera view.", this);
            return;
        }

        instance = Instantiate(original);
        mesh = BuildPatch(site);
        terrain.terrainData = instance;
        terrainCollider.terrainData = instance;

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

    bool FindSite(out Vector3 site)
    {
        Vector3 size = original.size;
        float span = Mathf.Min(size.x, size.z);
        var camera = strategyCamera != null ? strategyCamera.GetComponent<Camera>() : null;
        float best = float.NegativeInfinity;
        site = default;

        Vector3 survey = new Vector3(size.x * .5f + 60f, 0f, size.z * .5f + 235f);
        survey.y = HeightAt(survey.x, survey.z) + 12f;
        Vector3 surveyEye = camera != null
            ? survey - transform.InverseTransformDirection(camera.transform.forward) * 235f : survey;
        Vector3 surveyWorld = transform.TransformPoint(surveyEye);

        // These scans are in terrain coordinates and work with both checked-in and rebaked TerrainData.
        for (float z = size.z * .5f + Mathf.Min(150f, span * .20f); z < Mathf.Min(size.z - 100f, size.z * .5f + 290f); z += 4f)
        for (float x = size.x * .5f - 150f; x < size.x * .5f + 300f; x += 5f)
        {
            float baseRadius = Mathf.Min(185f, span * .22f);
            if (new Vector2(x - size.x * .5f, z - size.z * .5f).sqrMagnitude < baseRadius * baseRadius)
                continue;
            float foot = HeightAt(x, z - SlopeSample);
            float middle = HeightAt(x, z);
            float back = HeightAt(x, z + Length + 8f);
            if (back < foot + 13f || middle < foot + 2f) continue;

            var point = new Vector3(x, foot, z);
            float framePenalty = 0f;
            if (camera != null)
            {
                Vector3 pointWorld = transform.TransformPoint(point + Vector3.up * 8f);
                Vector3 view = camera.WorldToViewportPoint(pointWorld);
                if (!InFrame(view) || !MouthVisible(camera.transform.position, point))
                {
                    // Evaluate a second pose reachable by panning toward the north slope.
                    view = camera.WorldToViewportPoint(pointWorld + camera.transform.position - surveyWorld);
                    framePenalty = 18f;
                    if (!InFrame(view) || !MouthVisible(surveyWorld, point)) framePenalty += 70f;
                }
                framePenalty += (Mathf.Abs(view.x - .52f) + Mathf.Abs(view.y - .68f)) * 34f;
            }

            float shoulder = Mathf.Abs(HeightAt(x - HalfWidth, z) - HeightAt(x + HalfWidth, z));
            float score = -Mathf.Abs(HeightAt(x, z + SlopeSample) - foot - Crown)
                - shoulder * .4f - framePenalty - Mathf.Abs(middle - foot - 7f) * .45f;
            if (score <= best) continue;
            best = score;
            site = point;
        }
        return !float.IsNegativeInfinity(best);
    }

    static bool InFrame(Vector3 view) => view.z > 0f && view.x > .07f && view.x < .93f &&
        view.y > .08f && view.y < .94f;

    bool MouthVisible(Vector3 cameraPosition, Vector3 mouth)
    {
        Vector3 start = transform.InverseTransformPoint(cameraPosition);
        for (int i = 1; i <= 20; i++)
        {
            Vector3 p = Vector3.Lerp(start, mouth + Vector3.up * 8f, i / 21f);
            if (p.z < mouth.z - 34f && p.y < HeightAt(p.x, p.z) + 1f) return false;
        }
        return true;
    }

    float HeightAt(float x, float z) => original.GetInterpolatedHeight(x / original.size.x, z / original.size.z);

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
            float across = 1f - Mathf.SmoothStep(0f, 1f, (Mathf.Abs(px) - 23f) / 19f);
            float approach = Mathf.SmoothStep(0f, 1f, (pz + 59f) / 29f);
            float rear = 1f - Mathf.SmoothStep(0f, 1f, (pz - 1f) / 17f);
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
            if (px * px + pz * pz >= 1f || !holes[z, x]) continue;
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
            float taper = Mathf.Lerp(1f, .66f, t);
            points.Add(new Vector3(lip.x * taper + Mathf.Sin(t * 2.7f) * 7f,
                lip.y * (taper + noise * t * .21f) - t * 2f,
                Mathf.Lerp(lip.z, Length, t) + noise * t * 2f));
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
        float ledge = Mathf.Sin(angle * 9f) * .045f;
        return new Vector3(x * HalfWidth * (1f + noise * .12f + ledge),
            Mathf.Max(0f, z) * Crown * (1f + noise * .25f + ledge) - 1.3f,
            z * PortalRun * (1f + noise * .11f));
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
