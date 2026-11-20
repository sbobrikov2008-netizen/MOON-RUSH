#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public static class MoonRushWorldGenerator
{
    const string Menu = "MOON RUSH/Apply Lunar Landscape";
    const string ScenePath = "Assets/Moon.unity";
    const string Root = "Assets/MoonRushGenerated";
    const string LandscapePath = Root + "/MoonLandscape.asset";
    const int Resolution = 1025;
    const int SurfaceResolution = 512;
    const float HalfSize = 800f;
    const float Height = 190f;
    const float BaseHeight = 10f;
    const float BuildRadius = 155f;
    const string UrpTerrainMaterial =
        "Packages/com.unity.render-pipelines.universal/Runtime/Materials/TerrainLit.mat";

    // Separate, broad massifs leave saddles and low ground between the ridges.
    static readonly (Vector2 center, Vector2 width, float height)[] Ridges =
    {
        (new Vector2(40, 515), new Vector2(245, 95), 82),
        (new Vector2(280, 630), new Vector2(135, 175), 68),
        (new Vector2(490, 145), new Vector2(105, 235), 66),
        (new Vector2(380, -405), new Vector2(160, 125), 53),
        (new Vector2(-165, -510), new Vector2(225, 100), 76),
        (new Vector2(-510, -240), new Vector2(100, 165), 62),
        (new Vector2(-485, 295), new Vector2(140, 190), 71)
    };

    [MenuItem(Menu, true)]
    static bool CanGenerate()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode &&
            SceneManager.GetActiveScene().path == ScenePath;
    }

    [MenuItem(Menu)]
    public static void Generate()
    {
        if (!CanGenerate()) return;

        Scene scene = SceneManager.GetActiveScene();
        var terrains = System.Array.FindAll(
            Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None),
            candidate => candidate.gameObject.scene == scene);
        if (terrains.Length != 1 || terrains[0].terrainData == null ||
            !terrains[0].TryGetComponent(out TerrainCollider collider))
        {
            Debug.LogError("MOON RUSH: Moon.unity must contain one Terrain with TerrainData and a TerrainCollider.");
            return;
        }

        var layers = new TerrainLayer[3];
        string[] layerNames = { "Dust", "DarkRock", "LightEjecta" };
        for (int index = 0; index < layers.Length; index++)
            layers[index] = AssetDatabase.LoadAssetAtPath<TerrainLayer>(
                Root + "/" + layerNames[index] + ".terrainlayer");
        var rockMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/LunarRock.mat");
        if (System.Array.Exists(layers, layer => layer == null) || rockMaterial == null)
        {
            Debug.LogError("MOON RUSH: The regolith layers or LunarRock material are missing.");
            return;
        }

        var terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>(UrpTerrainMaterial);
        if (!IsSupportedShader(terrainMaterial, "Universal Render Pipeline/Terrain/Lit") ||
            !IsSupportedShader(rockMaterial, "Universal Render Pipeline/Lit"))
        {
            Debug.LogError("MOON RUSH: URP Terrain Lit and URP Lit must be available and supported. Check the active URP asset and Package Manager.");
            return;
        }

        if (!EditorUtility.DisplayDialog("MOON RUSH",
            "Apply the new relief, regolith and rocks to the existing Terrain in Moon.unity? The original TerrainData asset is preserved.",
            "Apply Landscape", "Cancel")) return;

        Terrain terrain = terrains[0];
        var previousBake = AssetDatabase.LoadMainAssetAtPath(LandscapePath);
        if (previousBake != null && !(previousBake is TerrainData))
        {
            Debug.LogError("MOON RUSH: Move the existing non-TerrainData asset at " + LandscapePath + " and try again.");
            return;
        }

        var craters = CreateCraters();
        float[,] heights = CreateHeights(craters);
        var data = previousBake as TerrainData;
        if (data == null)
        {
            data = Object.Instantiate(terrain.terrainData);
            AssetDatabase.CreateAsset(data, LandscapePath);
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply Lunar Landscape");
        Undo.RegisterCompleteObjectUndo(data, "Update lunar relief");
        Undo.RecordObjects(new Object[] { terrain, terrain.transform, collider }, "Update lunar Terrain");

        data.heightmapResolution = Resolution;
        data.size = new Vector3(2f * HalfSize, Height, 2f * HalfSize);
        data.SetHeights(0, 0, heights);
        PaintSurface(data, layers, craters);

        terrain.terrainData = data;
        collider.terrainData = data;
        terrain.transform.position = new Vector3(-HalfSize, 0f, -HalfSize);
        terrain.materialTemplate = terrainMaterial;
        terrain.drawInstanced = true;
        terrain.heightmapPixelError = 3;
        terrain.basemapDistance = 460f;
        terrain.Flush();
        GenerateRocks(terrain, rockMaterial);

        Undo.CollapseUndoOperations(undoGroup);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = terrain.gameObject;
        Debug.Log("MOON RUSH: Landscape applied. Save Moon.unity with Cmd+S and enter Play mode.");
    }

    static bool IsSupportedShader(Material material, string shaderName)
    {
        return material != null && material.shader != null &&
            material.shader.name == shaderName && material.shader.isSupported;
    }

    readonly struct Crater
    {
        public readonly Vector2 Center;
        public readonly float Radius;
        public readonly float Depth;

        public Crater(Vector2 center, float radius, float depth)
        {
            Center = center;
            Radius = radius;
            Depth = depth;
        }
    }

    static Crater[] CreateCraters()
    {
        var random = new System.Random(2611);
        var craters = new Crater[58];
        for (int index = 0; index < craters.Length; index++)
        {
            Vector2 center;
            float radius = Mathf.Lerp(8f, 42f, Mathf.Pow(Next(random), 2.1f));
            do
            {
                center = new Vector2(Next(random) * 2f - 1f, Next(random) * 2f - 1f) * 690f;
            }
            while (Mathf.Sqrt(Mathf.Pow(center.x / 1.17f, 2f) + center.y * center.y) <
                BuildRadius + 40f + radius * 1.55f);

            craters[index] = new Crater(center, radius, radius * Mathf.Lerp(.085f, .145f, Next(random)));
        }
        return craters;
    }

    static float[,] CreateHeights(Crater[] craters)
    {
        var heights = new float[Resolution, Resolution];
        for (int z = 0; z < Resolution; z++)
        for (int x = 0; x < Resolution; x++)
        {
            Vector2 point = Point(x / (float)(Resolution - 1), z / (float)(Resolution - 1));
            float region = BuildBlend(point);
            float broad = Noise(point, .0035f, 19f) - .5f;
            float detail = Noise(point, .018f, 61f) - .5f;
            float fine = Noise(point, .067f, 113f) - .5f;
            Vector2 warped = point + 96f * new Vector2(
                Noise(point, .003f, 133f) - .5f, Noise(point, .003f, 161f) - .5f);

            float upland = 0f;
            foreach (var ridge in Ridges)
            {
                Vector2 distance = warped - ridge.center;
                float u = distance.x / ridge.width.x;
                float v = distance.y / ridge.width.y;
                float falloff = 1f - (u * u + v * v) / 4f;
                if (falloff <= 0f) continue;
                upland += ridge.height * falloff * falloff * falloff;
            }

            float terrace = broad * .65f + detail * .16f + point.x * .0015f;
            float relief = broad * 17f + detail * 4.7f + fine * 1.2f +
                upland * (.82f + broad * .54f + detail * .26f);
            float elevation = BaseHeight + Mathf.Lerp(terrace, relief, region);
            heights[z, x] = Mathf.Clamp01(elevation / Height);
        }

        // Stamp localized craters after shaping the larger terrain.
        const float spacing = 2f * HalfSize / (Resolution - 1);
        foreach (var crater in craters)
        {
            float reach = crater.Radius * 1.55f;
            int minX = Mathf.Max(0, Mathf.FloorToInt((crater.Center.x + HalfSize - reach) / spacing));
            int maxX = Mathf.Min(Resolution - 1, Mathf.CeilToInt((crater.Center.x + HalfSize + reach) / spacing));
            int minZ = Mathf.Max(0, Mathf.FloorToInt((crater.Center.y + HalfSize - reach) / spacing));
            int maxZ = Mathf.Min(Resolution - 1, Mathf.CeilToInt((crater.Center.y + HalfSize + reach) / spacing));
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 point = new Vector2(x * spacing - HalfSize, z * spacing - HalfSize);
                float radius = CraterDistance(point, crater);
                float floor = 1f - Smooth(.19f, 1f, radius);
                float rim = 1f - Smooth(0f, .35f, Mathf.Abs(radius - .97f));
                float offset = crater.Depth * (.30f * rim - floor);
                heights[z, x] = Mathf.Clamp01(heights[z, x] + offset / Height);
            }
        }
        return heights;
    }

    static void PaintSurface(TerrainData data, TerrainLayer[] layers, Crater[] craters)
    {
        data.terrainLayers = layers;
        data.alphamapResolution = SurfaceResolution;
        var weights = new float[SurfaceResolution, SurfaceResolution, layers.Length];
        for (int z = 0; z < SurfaceResolution; z++)
        for (int x = 0; x < SurfaceResolution; x++)
        {
            float u = (x + .5f) / SurfaceResolution;
            float v = (z + .5f) / SurfaceResolution;
            Vector2 point = Point(u, v);
            float patch = Noise(point, .013f, 239f);
            float slope = data.GetSteepness(u, v);
            float rock = Smooth(11f, 34f, slope + (patch - .5f) * 12f) * .78f;
            float ejecta = Smooth(.54f, .8f, patch) * .23f;
            foreach (var crater in craters)
            {
                Vector2 delta = point - crater.Center;
                float reach = crater.Radius * 1.8f;
                if (delta.sqrMagnitude > reach * reach) continue;
                float radius = CraterDistance(point, crater);
                float rim = Smooth(.55f, .96f, radius) * (1f - Smooth(1.02f, 1.65f, radius));
                ejecta = Mathf.Max(ejecta, rim * Mathf.Lerp(.18f, .56f, patch));
            }
            ejecta *= 1f - rock;
            weights[z, x, 0] = 1f - rock - ejecta;
            weights[z, x, 1] = rock;
            weights[z, x, 2] = ejecta;
        }
        data.SetAlphamaps(0, 0, weights);
    }

    static void GenerateRocks(Terrain terrain, Material material)
    {
        Transform old = terrain.transform.Find("MoonRush_Rocks");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var root = new GameObject("MoonRush_Rocks");
        SceneManager.MoveGameObjectToScene(root, terrain.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(root, "Create lunar rock group");
        Undo.SetTransformParent(root.transform, terrain.transform, "Attach lunar rock group");
        Undo.RecordObject(root.transform, "Center lunar rock group");
        root.transform.localPosition = Vector3.zero;

        var random = new System.Random(8471);
        var meshes = new Mesh[4];
        for (int index = 0; index < meshes.Length; index++)
            meshes[index] = GetRockMesh(index);

        for (int index = 0; index < 1250; index++)
        {
            Vector2 point = Point(Next(random), Next(random)) * .91f;
            if (BuildBlend(point) < .72f) continue;
            float cluster = Noise(point, .010f, 307f);
            if (Next(random) > Mathf.Lerp(.22f, .85f, Smooth(.30f, .72f, cluster))) continue;
            float u = (point.x + HalfSize) / (2f * HalfSize);
            float v = (point.y + HalfSize) / (2f * HalfSize);
            TerrainData data = terrain.terrainData;
            if (data.GetSteepness(u, v) > 32f) continue;

            float size = Mathf.Lerp(.5f, 7f, Mathf.Pow(Next(random), 3.2f));
            var rock = new GameObject("Lunar Rock", typeof(MeshFilter), typeof(MeshRenderer));
            rock.transform.SetParent(root.transform, false);
            rock.transform.localPosition = new Vector3(point.x + HalfSize,
                data.GetInterpolatedHeight(u, v) - size * .08f, point.y + HalfSize);
            rock.transform.localRotation = Quaternion.FromToRotation(Vector3.up, data.GetInterpolatedNormal(u, v)) *
                Quaternion.Euler(0f, Next(random) * 360f, 0f);
            rock.transform.localScale = new Vector3(size, size * Mathf.Lerp(.48f, .94f, Next(random)),
                size * Mathf.Lerp(.62f, 1.1f, Next(random)));
            rock.GetComponent<MeshFilter>().sharedMesh = meshes[random.Next(meshes.Length)];
            var renderer = rock.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = size >= 1.5f ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            Undo.RegisterCreatedObjectUndo(rock, "Place lunar rock");
        }
    }

    static Mesh GetRockMesh(int variant)
    {
        string path = Root + "/MoonLandscapeRock" + variant + ".asset";
        var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (saved != null) return saved;

        const int sides = 6;
        var random = new System.Random(900 + variant);
        var lower = new Vector3[sides];
        var upper = new Vector3[sides];
        for (int index = 0; index < sides; index++)
        {
            float angle = index * Mathf.PI * 2f / sides;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            lower[index] = radial * Mathf.Lerp(.41f, .62f, Next(random)) + Vector3.down * .23f;
            upper[index] = radial * Mathf.Lerp(.25f, .51f, Next(random)) +
                Vector3.up * Mathf.Lerp(.16f, .49f, Next(random));
        }

        var vertices = new System.Collections.Generic.List<Vector3>(sides * 12);
        Vector3 crown = new Vector3(.06f, .51f, -.08f);
        for (int index = 0; index < sides; index++)
        {
            int next = (index + 1) % sides;
            vertices.AddRange(new[] { lower[index], upper[index], lower[next],
                lower[next], upper[index], upper[next],
                upper[index], crown, upper[next],
                lower[index], lower[next], Vector3.down * .26f });
        }

        var triangles = new int[vertices.Count];
        for (int index = 0; index < triangles.Length; index++) triangles[index] = index;
        var mesh = new Mesh { name = "Lunar rock " + variant };
        mesh.SetVertices(vertices);
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static Vector2 Point(float u, float v)
    {
        return new Vector2(u * 2f - 1f, v * 2f - 1f) * HalfSize;
    }

    static float BuildBlend(Vector2 point)
    {
        return Smooth(BuildRadius, BuildRadius + 70f, new Vector2(point.x / 1.17f, point.y).magnitude);
    }

    static float CraterDistance(Vector2 point, Crater crater)
    {
        float rimVariation = Mathf.Lerp(.91f, 1.09f, Noise(point, .047f, 185f));
        return (point - crater.Center).magnitude / (crater.Radius * rimVariation);
    }

    static float Smooth(float low, float high, float value)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(low, high, value));
    }

    static float Noise(Vector2 point, float frequency, float offset)
    {
        return Mathf.Clamp01(Mathf.PerlinNoise(point.x * frequency + offset,
            point.y * frequency + offset * .71f));
    }

    static float Next(System.Random random)
    {
        return (float)random.NextDouble();
    }
}
#endif
