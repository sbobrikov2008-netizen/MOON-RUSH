using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider))]
public sealed class LunarLandingPad : MonoBehaviour
{
    [SerializeField] Material urpLitTemplate;

    Mesh mesh;
    Material[] materials;
    readonly List<Vector3> vertices = new List<Vector3>();
    readonly List<int>[] faces = {
        new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>()
    };

    void Awake()
    {
        if (urpLitTemplate == null) return;
        // Low, recessed base, machined rim and a dark deck broken into concentric panels.
        Ring(0, 0f, 17.8f, -1.45f, .05f, 8);
        Ring(1, 0f, 17.4f, .05f, .42f, 8);
        Ring(0, 0f, 16.45f, .42f, .61f, 8);
        Ring(1, 15.6f, 16.5f, .61f, .69f, 8);
        Ring(2, 12.65f, 12.93f, .61f, .63f, 64);
        Ring(1, 6.55f, 6.75f, .61f, .627f, 48);
        Ring(2, 0f, 1.05f, .61f, .632f, 32);

        // Landing glyph, centerline marks and eight instrument housings with recessed lamps.
        Box(2, new Vector3(-2.15f, .638f, 0f), new Vector3(.64f, .025f, 6.8f), 0f);
        Box(2, new Vector3(2.15f, .638f, 0f), new Vector3(.64f, .025f, 6.8f), 0f);
        Box(2, new Vector3(0f, .639f, 0f), new Vector3(4.4f, .026f, .58f), 0f);
        for (int side = 0; side < 8; side++)
        {
            float angle = side * 45f;
            Quaternion bearing = Quaternion.Euler(0f, angle, 0f);
            Vector3 Axis(float radius, float height, float offset = 0f) =>
                bearing * new Vector3(offset, height, radius);
            Box(1, Axis(15f, .65f), new Vector3(3.7f, .13f, 1.05f), angle);
            Box(3, Axis(15f, .733f), new Vector3(2.9f, .032f, .12f), angle);
            Box(5, Axis(15.98f, .755f), new Vector3(1.4f, .16f, .64f), angle);
            Box(4, Axis(15.98f, .854f), new Vector3(.96f, .054f, .27f), angle);
            Box(2, Axis(10.9f, .635f), new Vector3(.26f, .025f, .86f), angle);
            Box(1, Axis(9.1f, .627f), new Vector3(2.3f, .022f, .07f), angle);
            for (int grille = -2; grille <= 2; grille++)
                Box(5, Axis(14.55f, .728f, grille * .34f), new Vector3(.17f, .025f, .31f), angle);
            for (int strip = -2; strip <= 2; strip++)
                Box(3, Axis(14.45f, .635f, strip * .34f), new Vector3(.115f, .025f, .53f), angle + 26f);
        }

        mesh = new Mesh { name = "Landing pad · deck, framing and guidance lights" };
        mesh.SetVertices(vertices);
        mesh.subMeshCount = faces.Length;
        for (int i = 0; i < faces.Length; i++) mesh.SetTriangles(faces[i], i);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        materials = new[]
        {
            Metal("Graphite deck", new Color(.18f, .21f, .24f), .55f, .36f),
            Metal("Brushed frame", new Color(.36f, .43f, .48f), .68f, .44f),
            Metal("Ceramic markings", new Color(.86f, .89f, .86f), .05f, .39f),
            Metal("Safety amber", new Color(.88f, .46f, .10f), .22f, .36f),
            Metal("Guidance light", new Color(.19f, .8f, .91f), 0f, .49f),
            Metal("Recessed fittings", new Color(.063f, .082f, .097f), .26f, .33f)
        };
        materials[4].EnableKeyword("_EMISSION");
        materials[4].SetColor("_EmissionColor", new Color(.17f, .86f, 1f) * 1.9f);
        GetComponent<MeshFilter>().sharedMesh = mesh;
        GetComponent<MeshRenderer>().sharedMaterials = materials;
        var collider = GetComponent<BoxCollider>();
        collider.center = new Vector3(0f, -.3f, 0f);
        collider.size = new Vector3(33f, 2.3f, 33f);
    }

    Material Metal(string label, Color color, float metal, float gloss)
    {
        var surface = new Material(urpLitTemplate) { name = label };
        surface.SetColor("_BaseColor", color);
        surface.SetFloat("_Metallic", metal);
        surface.SetFloat("_Smoothness", gloss);
        return surface;
    }

    void Ring(int material, float inner, float outer, float bottom, float top, int segments)
    {
        Vector3 Point(float radius, float angle, float y) =>
            new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
        for (int i = 0; i < segments; i++)
        {
            float a = Mathf.PI * 2f * (i + .5f) / segments;
            float b = a + Mathf.PI * 2f / segments;
            Vector3 oa = Point(outer, a, top);
            Vector3 ob = Point(outer, b, top);
            Vector3 ia = Point(inner, a, top);
            Vector3 ib = Point(inner, b, top);
            if (inner > 0f) Quad(material, ia, ib, ob, oa);
            else Triangle(material, ia, ob, oa);
            Quad(material, oa, ob, Point(outer, b, bottom), Point(outer, a, bottom));
            if (inner > 0f)
                Quad(material, ib, ia, Point(inner, a, bottom), Point(inner, b, bottom));
        }
    }

    void Box(int material, Vector3 center, Vector3 size, float yaw)
    {
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 P(float x, float y, float z) => center + rotation * Vector3.Scale(size, new Vector3(x, y, z)) * .5f;
        Vector3 a = P(-1f, 1f, -1f), b = P(-1f, 1f, 1f), c = P(1f, 1f, 1f), d = P(1f, 1f, -1f);
        Vector3 e = P(-1f, -1f, -1f), f = P(-1f, -1f, 1f), g = P(1f, -1f, 1f), h = P(1f, -1f, -1f);
        Quad(material, a, b, c, d);
        Quad(material, b, a, e, f);
        Quad(material, c, b, f, g);
        Quad(material, d, c, g, h);
        Quad(material, a, d, h, e);
    }

    void Quad(int material, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        Triangle(material, a, b, c);
        Triangle(material, a, c, d);
    }

    void Triangle(int material, Vector3 a, Vector3 b, Vector3 c)
    {
        faces[material].Add(vertices.Count);
        faces[material].Add(vertices.Count + 1);
        faces[material].Add(vertices.Count + 2);
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (materials == null) return;
        foreach (Material surface in materials) Destroy(surface);
    }
}
