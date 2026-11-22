using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Terrain), typeof(TerrainCollider))]
public sealed class MoonRushCave : MonoBehaviour
{
    [SerializeField] Material rockMaterial;
    [SerializeField] Texture2D regolith;
    [SerializeField] MoonRushCamera strategyCamera;

    const float HalfWidth = 16f;
    const float Crown = 18f;
    const float Length = 53f;
    const float PortalRun = 4.5f;
    const float SlopeSample = 12f;
    const int BlendingRings = 12;
    const int TunnelRings = 11;

    Terrain terrain;
    TerrainCollider terrainCollider;
    TerrainData original;
    TerrainData instance;
    Mesh mesh;
    Material surfaceMaterial;
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

        surfaceMaterial = new Material(rockMaterial) { name = "Cave slope regolith" };
        if (regolith != null)
        {
            surfaceMaterial.SetTexture("_BaseMap", regolith);
            surfaceMaterial.SetColor("_BaseColor", Color.white);
        }
        interiorMaterial = new Material(rockMaterial) { name = "Deep lunar basalt" };
        interiorMaterial.SetColor("_BaseColor", rockMaterial.GetColor("_BaseColor") * .52f);

        var cave = new GameObject("Lunar cave", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        cave.transform.SetParent(transform, false);
        cave.transform.localPosition = site;
        cave.GetComponent<MeshFilter>().sharedMesh = mesh;
        cave.GetComponent<MeshRenderer>().sharedMaterials = new[] { surfaceMaterial, rockMaterial, interiorMaterial };
        cave.GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    void OnDestroy()
    {
        if (instance == null) return;
        if (terrain != null && terrain.terrainData == instance) terrain.terrainData = original;
        if (terrainCollider != null && terrainCollider.terrainData == instance) terrainCollider.terrainData = original;
        Destroy(instance);
        Destroy(mesh);
        Destroy(surfaceMaterial);
        Destroy(interiorMaterial);
    }

    bool FindSite(out Vector3 site)
    {
        Vector3 size = original.size;
        float span = Mathf.Min(size.x, size.z);
        var camera = strategyCamera != null ? strategyCamera.GetComponent<Camera>() : null;
        float best = float.NegativeInfinity;
        site = default;

        Vector3 survey = new Vector3(size.x * .5f + 60f, 0f, size.z * .5f + 145f);
        survey.y = HeightAt(survey.x, survey.z) + 12f;
        Vector3 surveyEye = camera != null
            ? survey - transform.InverseTransformDirection(camera.transform.forward) * 195f : survey;
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
        int x0 = Mathf.FloorToInt((site.x - 40f) / dx);
        int x1 = Mathf.CeilToInt((site.x + 40f) / dx);
        int z0 = Mathf.FloorToInt((site.z - 34f) / dz);
        int z1 = Mathf.CeilToInt((site.z + Length + 30f) / dz);
        var boundary = new List<Vector2>();
        for (int x = x0; x < x1; x++) boundary.Add(new Vector2(x * dx, z0 * dz));
        for (int z = z0; z < z1; z++) boundary.Add(new Vector2(x1 * dx, z * dz));
        for (int x = x1; x > x0; x--) boundary.Add(new Vector2(x * dx, z1 * dz));
        for (int z = z1; z > z0; z--) boundary.Add(new Vector2(x0 * dx, z * dz));

        // Use the exact Terrain height samples along the replacement perimeter.
        // Every removed cell is covered by either the reconstructed slope or the cave.
        int count = boundary.Count;
        var points = new List<Vector3>();
        var surface = new List<int>();
        var rock = new List<int>();
        var dark = new List<int>();
        var portal = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            Vector2 delta = boundary[i] - new Vector2(site.x, site.z);
            Vector2 radial = new Vector2(delta.x / HalfWidth, delta.y / PortalRun).normalized;
            float noise = Mathf.PerlinNoise(radial.x * 3.1f + 11f, radial.y * 3.1f + 20f) - .5f;
            float slabs = Mathf.Sin(Mathf.Atan2(radial.y, radial.x) * 9f) * .045f;
            float x = radial.x * HalfWidth * (1f + noise * .24f + slabs);
            float y = Mathf.Max(0f, radial.y) * Crown * (1f + noise * .22f + slabs);
            float z = radial.y * PortalRun + noise * 2f;
            portal[i] = new Vector3(x, y, z);
        }

        for (int ring = 0; ring <= BlendingRings; ring++)
        for (int i = 0; i < count; i++)
        {
            float t = ring / (float)BlendingRings;
            Vector3 lip = portal[i];
            Vector2 outer = boundary[i];
            float x = Mathf.Lerp(outer.x - site.x, lip.x, t);
            float z = Mathf.Lerp(outer.y - site.z, lip.z, t);
            float ground = HeightAt(site.x + x, site.z + z) - site.y;
            float shelf = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Abs(x) / 17f);
            float roof = Mathf.Lerp(Crown + 2f, Crown * .78f, Mathf.Clamp01(z / Length));
            if (z > PortalRun && z < Length + 6f)
            {
                float taper = Mathf.SmoothStep(0f, 1f, (z - PortalRun) / 9f) *
                    (1f - Mathf.SmoothStep(0f, 1f, (z - Length) / 6f));
                ground += Mathf.Max(0f, roof - ground) * shelf * taper;
            }
            float detail = Mathf.PerlinNoise(x * .3f + 17f, z * .31f + 28f) - .5f;
            float ridge = Mathf.Sin(x * .39f + z * .24f) * .9f;
            float flank = Mathf.Exp(-Mathf.Pow((Mathf.Abs(x) - 18f) / 7f, 2f)
                - Mathf.Pow((z - 4f) / 10f, 2f));
            float slabs = flank * (5.8f + detail * 4.5f);
            float looseRock = (detail * 3.7f + ridge + slabs) * Mathf.Sin(t * Mathf.PI) * t;
            float weight = Mathf.SmoothStep(0f, 1f, t);
            points.Add(new Vector3(x, Mathf.Lerp(ground, lip.y, weight) + looseRock, z));
        }

        for (int ring = 0; ring < BlendingRings; ring++)
        for (int i = 0; i < count; i++)
        {
            int a = ring * count + i;
            int b = ring * count + (i + 1) % count;
            AddQuad(ring >= BlendingRings - 3 ? rock : surface, a, a + count, b + count, b);
        }

        for (int ring = 1; ring <= TunnelRings; ring++)
        for (int i = 0; i < count; i++)
        {
            float t = ring / (float)TunnelRings;
            Vector3 lip = portal[i];
            float noise = Mathf.PerlinNoise(lip.x * .15f + ring * .43f + 16f, lip.y * .19f + 5f) - .5f;
            float width = Mathf.Lerp(1f, .62f, t);
            float x = lip.x * width + Mathf.Sin(t * 2.7f) * 5.5f;
            float y = lip.y * (width + noise * t * .23f) - t * 1.7f;
            float z = Mathf.Lerp(lip.z, Length, t) + noise * t * 1.8f;
            points.Add(new Vector3(x, y, z));
        }
        for (int ring = 0; ring < TunnelRings; ring++)
        for (int i = 0; i < count; i++)
        {
            int a = (BlendingRings + ring) * count + i;
            int b = (BlendingRings + ring) * count + (i + 1) % count;
            AddQuad(ring == 0 ? rock : dark, a, a + count, b + count, b);
        }
        int end = (BlendingRings + TunnelRings) * count;
        int cap = points.Count;
        points.Add(new Vector3(2f, 4.5f, Length + 2f));
        for (int i = 0; i < count; i++)
        {
            dark.Add(end + (i + 1) % count);
            dark.Add(end + i);
            dark.Add(cap);
        }

        var uv = new List<Vector2>(points.Count);
        foreach (Vector3 p in points)
            uv.Add(new Vector2((site.x + p.x + p.y * .37f) / 32f, (site.z + p.z + p.y * .5f) / 32f));

        var result = new Mesh { name = "Lunar slope, cavern arch and tunnel" };
        result.SetVertices(points);
        result.SetUVs(0, uv);
        result.subMeshCount = 3;
        result.SetTriangles(surface, 0);
        result.SetTriangles(rock, 1);
        result.SetTriangles(dark, 2);
        result.RecalculateNormals();
        result.RecalculateBounds();
        instance.SetHoles(x0, z0, new bool[z1 - z0, x1 - x0]);
        return result;
    }

    static void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a); triangles.Add(b); triangles.Add(c);
        triangles.Add(a); triangles.Add(c); triangles.Add(d);
    }
}
