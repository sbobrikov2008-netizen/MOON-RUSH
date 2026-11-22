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
        // Eight heavy foundation sectors carry a bevelled ring and the flush rocket deck.
        Ring(5, 0f, 20.6f, -3.38f, -.57f, 8);
        Ring(1, 0f, 20.35f, -.57f, .13f, 8);
        Ring(0, 0f, 19.82f, .13f, .49f, 8);
        Ring(1, 18.75f, 20.07f, .25f, .63f, 8);
        Ring(5, 0f, 7.8f, .48f, .515f, 48);
        Ring(2, 12.75f, 12.99f, .49f, .514f, 80);
        Ring(3, 8.08f, 8.21f, .49f, .514f, 64);
        Ring(2, 2.9f, 3.1f, .515f, .536f, 40);

        for (int side = 0; side < 8; side++)
        {
            float angle = side * 45f;
            Quaternion bearing = Quaternion.Euler(0f, angle, 0f);
            Vector3 Axis(float radius, float height, float offset = 0f) =>
                bearing * new Vector3(offset, height, radius);

            // Shallow markings, radial expansion joints and recessed service hatches.
            Box(2, Axis(10.38f, .515f), new Vector3(.26f, .023f, 1.26f), angle);
            Box(2, Axis(14.48f, .515f), new Vector3(.25f, .024f, 1.82f), angle);
            Box(5, Axis(16.48f, .507f, 2.85f), new Vector3(.095f, .016f, 4.25f), angle + 12f);
            Box(1, Axis(17.62f, .52f), new Vector3(4.5f, .075f, 1.4f), angle);
            Box(5, Axis(17.4f, .567f), new Vector3(2.7f, .022f, .63f), angle);
            for (int bolt = -2; bolt <= 2; bolt++)
            {
                Box(1, Axis(17.31f, .593f, bolt * .43f), new Vector3(.1f, .058f, .1f), angle);
                Box(3, Axis(18.33f, .512f, bolt * .34f), new Vector3(.12f, .025f, .54f), angle + 22f);
            }

            // Lateral service boxes and low amber/blue beacons outside the clearance area.
            Box(5, Axis(19.1f, .64f), new Vector3(2.6f, .22f, .83f), angle);
            Box(4, Axis(19.1f, .782f), new Vector3(1.28f, .064f, .24f), angle);
            if (side % 2 == 0)
            {
                Box(1, Axis(18.64f, .71f, 3.85f), new Vector3(1.58f, .47f, 1.07f), angle);
                Box(5, Axis(18.64f, .95f, 3.85f), new Vector3(1.37f, .05f, .75f), angle);
                Box(3, Axis(18.64f, .983f, 3.85f), new Vector3(.97f, .035f, .12f), angle);
            }
        }

        mesh = new Mesh { name = "Landing pad · deck, framing and guidance lights" };
        mesh.SetVertices(vertices);
        mesh.subMeshCount = faces.Length;
        for (int i = 0; i < faces.Length; i++) mesh.SetTriangles(faces[i], i);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        materials = new[]
        {
            Metal("Sintered landing deck", new Color(.25f, .27f, .29f), .21f, .27f),
            Metal("Brushed frame", new Color(.36f, .43f, .48f), .68f, .44f),
            Metal("Ceramic markings", new Color(.86f, .89f, .86f), .05f, .39f),
            Metal("Safety amber", new Color(.88f, .46f, .10f), .22f, .36f),
            Metal("Guidance light", new Color(.19f, .8f, .91f), 0f, .49f),
            Metal("Heat shield and foundation", new Color(.12f, .145f, .16f), .28f, .29f)
        };
        materials[4].EnableKeyword("_EMISSION");
        materials[4].SetColor("_EmissionColor", new Color(.17f, .86f, 1f) * 1.9f);
        GetComponent<MeshFilter>().sharedMesh = mesh;
        GetComponent<MeshRenderer>().sharedMaterials = materials;
        var collider = GetComponent<BoxCollider>();
        collider.center = new Vector3(0f, -1.19f, 0f);
        collider.size = new Vector3(38f, 4.38f, 38f);
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
