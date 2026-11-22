using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Terrain))]
public sealed class MoonRushHorizon : MonoBehaviour
{
    [SerializeField] Material rockMaterial;
    public const float Radius = 2100f;

    // Closely spaced rings at the Terrain join preserve its edge heights.
    static readonly float[] Offsets = { 0f, 2f, 7f, 15f, 27f, 42f, 60f, 80f, 104f, 132f, 164f, 200f,
        240f, 288f, 345f, 414f, 496f, 592f, 704f, 834f, 980f, 1140f, 1320f, 1500f };

    Mesh mesh;
    Material material;

    void Awake()
    {
        TerrainData data = GetComponent<Terrain>().terrainData;
        if (data == null || rockMaterial == null) return;

        int side = data.heightmapResolution - 1;
        int perimeter = side * 4;
        Vector3 size = data.size;
        var center = new Vector2(size.x, size.z) * .5f;
        var vertices = new Vector3[perimeter * Offsets.Length];
        var uv = new Vector2[vertices.Length];
        var seamNormals = new Vector3[perimeter];
        var triangles = new int[perimeter * (Offsets.Length - 1) * 6];
        Vector2 tile = new Vector2(32f, 32f);
        material = new Material(rockMaterial) { name = "Distant lunar regolith" };
        TerrainLayer[] layers = data.terrainLayers;
        if (layers.Length > 0 && layers[0] != null && layers[0].diffuseTexture != null)
        {
            tile = layers[0].tileSize;
            material.SetTexture("_BaseMap", layers[0].diffuseTexture);
            material.SetColor("_BaseColor", Color.white);
            if (layers[0].normalMapTexture != null)
            {
                material.SetTexture("_BumpMap", layers[0].normalMapTexture);
                material.SetFloat("_BumpScale", layers[0].normalScale);
                material.EnableKeyword("_NORMALMAP");
            }
        }

        for (int i = 0; i < perimeter; i++)
        {
            float t = i % side / (float)side;
            Vector2 edge;
            switch (i / side)
            {
                case 0: edge = new Vector2(t, 0f); break;
                case 1: edge = new Vector2(1f, t); break;
                case 2: edge = new Vector2(1f - t, 1f); break;
                default: edge = new Vector2(0f, 1f - t); break;
            }
            Vector2 shore = Vector2.Scale(edge, center * 2f);
            Vector2 radial = (shore - center).normalized;
            float height = data.GetInterpolatedHeight(edge.x, edge.y);
            seamNormals[i] = data.GetInterpolatedNormal(edge.x, edge.y);
            float blendStep = 1f / side;
            Vector2 inside = edge + (new Vector2(.5f, .5f) - edge) * blendStep * 2f;
            float insideHeight = data.GetInterpolatedHeight(inside.x, inside.y);
            float slope = Mathf.Clamp((height - insideHeight) /
                Vector2.Distance(shore, Vector2.Scale(inside, center * 2f)), -1.3f, 1.3f);
            float reach = Radius - Vector2.Distance(shore, center);

            for (int ring = 0; ring < Offsets.Length; ring++)
            {
                float distance = reach * Offsets[ring] / Offsets[Offsets.Length - 1];
                Vector2 point = shore + radial * distance;
                float near = height + slope * distance * Mathf.Exp(-distance / 28f);
                float broad = Mathf.PerlinNoise(point.x * .0017f + 12f, point.y * .0017f + 26f);
                float detail = Mathf.PerlinNoise(point.x * .006f + 38f, point.y * .006f + 51f);
                // Broken secondary massifs sit behind the playable mountains.
                float azimuth = Mathf.Atan2(radial.y, radial.x);
                float ridgeDistance = 118f + 46f * Mathf.Sin(azimuth * 3f + .68f)
                    + 25f * Mathf.Sin(azimuth * 7f - .4f);
                float ridgeProfile = Mathf.Exp(-Mathf.Pow((distance - ridgeDistance) / 82f, 2f));
                float breaks = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(.26f, .76f, Mathf.PerlinNoise(point.x * .0031f + 61f, point.y * .0031f + 17f)));
                float spine = 1f - Mathf.Abs(2f * detail - 1f);
                float rubble = Mathf.PerlinNoise(point.x * .021f + 15f, point.y * .021f + 34f);
                float outland = 11f + broad * broad * 73f + detail * 13f + rubble * 4f
                    + ridgeProfile * breaks * (23f + 69f * spine);
                // Compress only the highest distant peaks so the sky remains in frame.
                float excess = Mathf.Max(0f, outland - 118f);
                outland -= excess - excess / (1f + excess / 32f);
                // Roll the far ground down behind the ridges. The outer rim cannot
                // form an elevated, uniform skyline or a vertical skirt.
                float falloff = Mathf.SmoothStep(0f, 1f, (distance / reach - .63f) / .37f);
                outland = Mathf.Lerp(outland, -45f, falloff);
                // Keep the descending rim behind irregular foreground at this range.
                float lipCover = Mathf.Exp(-Mathf.Pow((distance / reach - .88f) / .045f, 2f));
                float coverHeight = 13f + 16f * broad;
                outland += Mathf.Max(0f, coverHeight - outland) * lipCover;
                float y = Mathf.Lerp(near, outland, Mathf.SmoothStep(0f, 1f, distance / 170f));
                int index = ring * perimeter + i;
                vertices[index] = new Vector3(point.x, y, point.y);
                uv[index] = new Vector2(point.x / Mathf.Max(.01f, tile.x), point.y / Mathf.Max(.01f, tile.y));
            }
        }

        int cursor = 0;
        for (int ring = 0; ring < Offsets.Length - 1; ring++)
        for (int i = 0; i < perimeter; i++)
        {
            int a = ring * perimeter + i;
            int b = ring * perimeter + (i + 1) % perimeter;
            triangles[cursor++] = a;
            triangles[cursor++] = b;
            triangles[cursor++] = b + perimeter;
            triangles[cursor++] = a;
            triangles[cursor++] = b + perimeter;
            triangles[cursor++] = a + perimeter;
        }

        mesh = new Mesh { name = "Continuous lunar horizon", indexFormat = IndexFormat.UInt32 };
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        var normals = mesh.normals;
        System.Array.Copy(seamNormals, normals, perimeter);
        mesh.normals = normals;
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        var backdrop = new GameObject("Distant lunar terrain", typeof(MeshFilter), typeof(MeshRenderer));
        backdrop.transform.SetParent(transform, false);
        backdrop.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = backdrop.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    void OnDestroy()
    {
        Destroy(mesh);
        Destroy(material);
    }
}
