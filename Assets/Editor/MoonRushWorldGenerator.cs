#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.IO;

public static class MoonRushWorldGenerator
{
    const int Resolution = 513;
    const float Size = 900f;
    const float Height = 190f;
    const string Root = "Assets/MoonRushGenerated";

    [MenuItem("MOON RUSH/Generate New Lunar World")]
    public static void Generate()
    {
        if (!EditorUtility.DisplayDialog("MOON RUSH", "This will replace the current Terrain heights and remove generated rocks. Save a backup scene first. Continue?", "Generate", "Cancel")) return;
        Directory.CreateDirectory(Root);
        var terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain == null)
        {
            TerrainData newData = new TerrainData();
            newData.heightmapResolution = Resolution;
            newData.size = new Vector3(Size, Height, Size);
            AssetDatabase.CreateAsset(newData, Root + "/LunarTerrain.asset");
            terrain = Terrain.CreateTerrainGameObject(newData).GetComponent<Terrain>();
        }
        TerrainData data = terrain.terrainData;
        if (!AssetDatabase.Contains(data))
        {
            TerrainData copy = Object.Instantiate(data);
            AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath(Root + "/LunarTerrain.asset"));
            terrain.terrainData = copy;
            data = copy;
        }
        Undo.RegisterCompleteObjectUndo(data, "Generate Lunar Terrain");
        Undo.RegisterCompleteObjectUndo(terrain.transform, "Center Lunar Terrain");
        data.heightmapResolution = Resolution;
        data.size = new Vector3(Size, Height, Size);
        terrain.transform.position = new Vector3(-Size / 2f, 0, -Size / 2f);
        var rng = new System.Random(2611);
        float[,] heights = new float[Resolution, Resolution];
        for (int z = 0; z < Resolution; z++)
        for (int x = 0; x < Resolution; x++)
        {
            float wx = (x / (float)(Resolution - 1) - .5f) * Size;
            float wz = (z / (float)(Resolution - 1) - .5f) * Size;
            float angle = Mathf.Atan2(wz, wx);
            float radius = Mathf.Sqrt(wx * wx + wz * wz);
            float edge = 300f + 30f * Mathf.Sin(angle * 3.2f + .8f) + 24f * Mathf.Sin(angle * 7.1f) + 11f * Mathf.Sin(angle * 13.4f);
            float ridge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((radius - edge) / 130f));
            float macro = Mathf.PerlinNoise(wx * .006f + 8f, wz * .006f + 19f);
            float detail = Mathf.PerlinNoise(wx * .026f + 50f, wz * .026f + 73f);
            float jagged = Mathf.Abs(Mathf.PerlinNoise(wx * .018f + 31f, wz * .018f + 17f) * 2f - 1f);
            float basin = 9f + (macro - .5f) * 2.5f + (detail - .5f) * 1.3f;
            float mountain = ridge * (32f + 47f * macro + 36f * jagged + 12f * detail);
            float elevation = basin + mountain;
            // Sparse, subtle craters across the central playable basin.
            for (int c = 0; c < 14; c++)
            {
                float cx = Mathf.Sin(c * 8.12f) * 240f;
                float cz = Mathf.Cos(c * 5.63f) * 235f;
                float r = 8f + (c % 5) * 5f;
                float d = Vector2.Distance(new Vector2(wx, wz), new Vector2(cx, cz));
                float bowl = Mathf.Exp(-Mathf.Pow(d / (r * .72f), 2f));
                float lip = Mathf.Exp(-Mathf.Pow((d - r) / (r * .22f), 2f));
                elevation += -1.9f * bowl + .75f * lip;
            }
            heights[z, x] = Mathf.Clamp01(elevation / Height);
        }
        data.SetHeights(0, 0, heights);
        terrain.drawInstanced = true;
        terrain.heightmapPixelError = 3;
        CreateSurfaceLayers(data);
        GenerateRocks(terrain, rng);
        SetupLighting();
        SetupCamera();
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = terrain.gameObject;
        EditorGUIUtility.PingObject(terrain);
        Debug.Log("MOON RUSH: lunar world generated. Save the scene with Cmd+S. Press Play to test the camera.");
    }

    static void CreateSurfaceLayers(TerrainData data)
    {
        // Textures are generated locally, so no external packages or downloads are needed.
        string texturePath = Root + "/LunarAlbedo.png";
        const int texSize = 512;
        var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        var colors = new Color[texSize * texSize];
        for (int y = 0; y < texSize; y++)
        for (int x = 0; x < texSize; x++)
        {
            float grain = Mathf.PerlinNoise(x * .22f + 2f, y * .22f + 4f);
            float patches = Mathf.PerlinNoise(x * .018f + 17f, y * .018f + 12f);
            float v = Mathf.Clamp01(.36f + (grain - .5f) * .12f + (patches - .5f) * .13f);
            colors[y * texSize + x] = new Color(v * 1.02f, v * 1.015f, v, 1f);
        }
        tex.SetPixels(colors);
        tex.Apply();
        File.WriteAllBytes(texturePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texturePath);
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer != null)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(Root + "/LunarLayer.terrainlayer");
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, Root + "/LunarLayer.terrainlayer");
        }
        layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        layer.tileSize = new Vector2(28f, 28f);
        EditorUtility.SetDirty(layer);
        data.terrainLayers = new[] { layer };
        var alpha = new float[data.alphamapHeight, data.alphamapWidth, 1];
        for (int y = 0; y < data.alphamapHeight; y++)
        for (int x = 0; x < data.alphamapWidth; x++) alpha[y, x, 0] = 1f;
        data.SetAlphamaps(0, 0, alpha);
    }

    static void GenerateRocks(Terrain terrain, System.Random rng)
    {
        Transform old = terrain.transform.Find("MoonRush_Rocks");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var root = new GameObject("MoonRush_Rocks");
        Undo.RegisterCreatedObjectUndo(root, "Generate lunar rocks");
        root.transform.SetParent(terrain.transform, false);
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/LunarRock.mat");
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            mat.color = new Color(.34f, .34f, .35f);
            AssetDatabase.CreateAsset(mat, Root + "/LunarRock.mat");
        }
        for (int i = 0; i < 180; i++)
        {
            float x = (float)(rng.NextDouble() - .5) * 840f;
            float z = (float)(rng.NextDouble() - .5) * 840f;
            float d = Mathf.Sqrt(x * x + z * z);
            if (d < 135f || (d < 260f && rng.NextDouble() < .78)) continue;
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Lunar Rock";
            rock.transform.SetParent(root.transform);
            float s = Mathf.Lerp(.7f, 5f, (float)rng.NextDouble());
            rock.transform.position = new Vector3(x, terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y + s * .3f, z);
            rock.transform.localScale = new Vector3(s * 1.5f, s * .65f, s * 1.1f);
            rock.transform.rotation = Quaternion.Euler((float)rng.NextDouble() * 20f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 25f);
            rock.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(rock.GetComponent<Collider>());
        }
    }

    static void SetupLighting()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.17f, .18f, .21f);
        RenderSettings.fog = false;
        Light sun = null;
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light.type == LightType.Directional) { sun = light; break; }
        if (sun == null)
        {
            var go = new GameObject("Lunar Sun");
            Undo.RegisterCreatedObjectUndo(go, "Create lunar sun");
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
        }
        sun.transform.rotation = Quaternion.Euler(38f, -32f, 0f);
        sun.color = new Color(1f, .98f, .95f);
        sun.intensity = 1.45f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = .92f;
    }

    static void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            Undo.RegisterCreatedObjectUndo(go, "Create game camera");
            cam = go.AddComponent<Camera>();
            go.tag = "MainCamera";
            go.AddComponent<AudioListener>();
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(.012f, .014f, .022f);
        cam.farClipPlane = 2500f;
        var controller = cam.GetComponent<MoonRushCamera>();
        if (controller == null) controller = cam.gameObject.AddComponent<MoonRushCamera>();
        controller.target = new Vector3(0f, 10f, 0f);
        controller.distance = 210f;
        controller.yaw = 35f;
        controller.pitch = 52f;
        controller.playableHalfSize = 245f;
        cam.transform.rotation = Quaternion.Euler(controller.pitch, controller.yaw, 0);
        cam.transform.position = controller.target - cam.transform.rotation * Vector3.forward * controller.distance;
        EditorUtility.SetDirty(cam.gameObject);
    }
}
#endif
