using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider))]
public sealed class LunarBaseModule : MonoBehaviour
{
    [SerializeField] BuildingKind kind;
    [SerializeField] Material urpLitTemplate;
    [SerializeField] Transform[] connectionPorts;

    public IReadOnlyList<Transform> ConnectionPorts => connectionPorts;

    const int Shell = 0;
    const int Frame = 1;
    const int Dark = 2;
    const int Trim = 3;
    const int Window = 4;
    const int Beacon = 5;

    readonly List<Vector3> vertices = new List<Vector3>();
    readonly List<int>[] faces = {
        new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>()
    };
    Mesh mesh;
    Material[] materials;

    void Awake()
    {
        bool habitat = kind == BuildingKind.Habitat;
        if (habitat) BuildHabitat();
        else BuildCommand();

        mesh = new Mesh { name = habitat ? "Habitat pressure modules" : "Command pressure hull" };
        mesh.SetVertices(vertices);
        mesh.subMeshCount = faces.Length;
        for (int i = 0; i < faces.Length; i++) mesh.SetTriangles(faces[i], i);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        vertices.Clear();
        foreach (List<int> face in faces) face.Clear();

        materials = new[]
        {
            Surface("Ceramic-coated pressure panels", new Color(.77f, .80f, .78f), .34f, .33f),
            Surface("Brushed aluminium frames", new Color(.33f, .42f, .47f), .63f, .46f),
            Surface("Graphite thermal shielding", new Color(.095f, .125f, .147f), .36f, .35f),
            Surface("Orange safety markings", new Color(.87f, .41f, .11f), .23f, .35f),
            Surface("Lit pressure windows", habitat ? new Color(.91f, .59f, .28f) : new Color(.21f, .61f, .81f), .16f, .55f),
            Surface("Approach beacons", new Color(.14f, .8f, .65f), .05f, .48f)
        };
        materials[Window].EnableKeyword("_EMISSION");
        materials[Window].SetColor("_EmissionColor",
            habitat ? new Color(.83f, .37f, .09f) * 1.14f : new Color(.09f, .49f, .77f) * 1.28f);
        materials[Beacon].EnableKeyword("_EMISSION");
        materials[Beacon].SetColor("_EmissionColor", new Color(.05f, .83f, .69f) * 1.6f);
        GetComponent<MeshFilter>().sharedMesh = mesh;
        GetComponent<MeshRenderer>().sharedMaterials = materials;
    }

    void BuildCommand()
    {
        Axial(Dark, Vector3.zero, Vector3.up, 8, (-2.65f, 11.1f), (-.24f, 11.1f), (.54f, 11.65f));
        Axial(Frame, Vector3.zero, Vector3.up, 8, (.46f, 11.67f), (.88f, 11.67f));
        Axial(Shell, Vector3.zero, Vector3.up, 8,
            (.88f, 11.4f), (1.65f, 11.55f), (5.75f, 11.55f), (6.8f, 10.1f), (7.07f, 9.7f));
        Axial(Dark, Vector3.zero, Vector3.up, 8, (6.83f, 9.75f), (7.25f, 9.75f), (7.66f, 8.9f));
        Axial(Frame, Vector3.zero, Vector3.up, 8, (7.62f, 8.6f), (7.86f, 8.39f));

        for (int side = 0; side < 8; side++)
        {
            Quaternion turn = Quaternion.Euler(0f, side * 45f, 0f);
            Vector3 P(float x, float y, float z) => turn * new Vector3(x, y, z);
            Box(Dark, P(0f, 4.62f, 10.7f), new Vector3(7.92f, 1.8f, .28f), turn);
            for (int pane = -2; pane <= 2; pane++)
                Box(Window, P(pane * 1.5f, 4.65f, 10.86f), new Vector3(1.31f, 1.34f, .15f), turn);
            Box(Frame, P(0f, 5.61f, 10.75f), new Vector3(8.06f, .25f, .51f), turn);
            Box(Frame, P(0f, 3.67f, 10.75f), new Vector3(8.06f, .15f, .42f), turn);
            for (int pillar = -1; pillar <= 1; pillar += 2)
            {
                Box(Frame, P(pillar * 4.15f, 2.68f, 10.68f), new Vector3(.38f, 3.55f, .43f), turn);
                Box(Trim, P(pillar * 4.15f, 2.1f, 10.93f), new Vector3(.31f, .39f, .07f), turn);
                Box(Beacon, P(pillar * 3.8f, 1.38f, 10.84f), new Vector3(.44f, .11f, .13f), turn);
            }
            for (int seam = -2; seam <= 2; seam++)
                Box(Frame, P(seam * 1.43f, 2.82f, 10.692f), new Vector3(.035f, 1.24f, .057f), turn);
        }

        Airlock(new Vector3(0f, 2.34f, 10.2f), Vector3.forward, 14.3f - 10.2f, 1.73f);
        Airlock(new Vector3(10.2f, 2.34f, 0f), Vector3.right, 3.7f, 1.36f);
        Airlock(new Vector3(-10.2f, 2.34f, 0f), Vector3.left, 3.7f, 1.36f);
        Box(Frame, new Vector3(0f, -1.01f, 12.68f), new Vector3(3.7f, 3.16f, 4.12f));
        Box(Dark, new Vector3(0f, .61f, 14.85f), new Vector3(3.7f, .3f, 1.94f));
        for (int step = 0; step < 5; step++)
            Box(Trim, new Vector3(-1.64f + step * .82f, .778f, 15.42f), new Vector3(.37f, .035f, .27f));

        for (int i = 0; i < 3; i++)
            Equipment(new Vector3(-7.25f + i * 2.6f, 7.88f, -3.84f), 1.65f, 1.6f);
        Vector3 mast = new Vector3(-4.95f, 7.87f, 3.4f);
        Beam(Frame, mast, mast + Vector3.up * 5.65f, .19f);
        for (int leg = 0; leg < 4; leg++)
        {
            float angle = leg * Mathf.PI / 2f;
            Beam(Frame, mast + new Vector3(Mathf.Cos(angle), .12f, Mathf.Sin(angle)),
                mast + Vector3.up * 2.2f, .13f);
        }
        for (int beam = 0; beam < 3; beam++)
        {
            Vector3 height = mast + Vector3.up * (3.25f + .72f * beam);
            Beam(Frame, height + Vector3.left * (1.4f - beam * .26f),
                height + Vector3.right * (1.4f - beam * .26f), .10f);
        }
        Box(Beacon, mast + Vector3.up * 5.63f, new Vector3(.22f, .28f, .22f));
        Radar(new Vector3(4.85f, 10.2f, 2.35f));
        Beam(Trim, new Vector3(0f, 7.94f, -4.6f), new Vector3(4.85f, 7.94f, -4.6f), .12f);
        Beam(Dark, new Vector3(4.85f, 7.94f, -4.6f), new Vector3(4.85f, 7.94f, 2.35f), .15f);
    }

    void BuildHabitat()
    {
        for (int pair = -1; pair <= 1; pair += 2)
        {
            float x = pair * 6f;
            Vector3 axis = new Vector3(x, 3.38f, 0f);
            Box(Dark, new Vector3(x, -1.05f, 0f), new Vector3(4.15f, 3.18f, 17.1f));
            foreach (float z in new[] { -6.1f, -.2f, 6.1f })
            {
                Box(Frame, new Vector3(x, .68f, z), new Vector3(5.45f, 1.63f, 2.18f));
                Box(Dark, new Vector3(x, .2f, z), new Vector3(5.8f, .27f, 2.48f));
            }

            Axial(Shell, axis, Vector3.forward, 28, (-9.74f, .75f), (-9.22f, 2.2f),
                (-8.1f, 3.02f), (-7.3f, 3.24f), (7.3f, 3.24f), (8.1f, 3.02f),
                (9.22f, 2.2f), (9.74f, .75f));
            foreach (float z in new[] { -8.35f, -6.2f, -3.25f, 3.25f, 6.2f, 8.35f })
            {
                float collar = Mathf.Abs(z) > 8 ? 2.97f : 3.285f;
                Axial(Frame, axis, Vector3.forward, 28, (z - .12f, collar), (z + .12f, collar));
            }
            for (int pane = -2; pane <= 2; pane++)
            {
                float z = pane * 2.36f;
                Box(Dark, new Vector3(x + pair * 3.13f, 3.82f, z), new Vector3(.28f, 1.22f, 1.87f));
                Box(Window, new Vector3(x + pair * 3.28f, 3.83f, z), new Vector3(.15f, .82f, 1.44f));
                Box(Frame, new Vector3(x + pair * 3.36f, 3.83f, z), new Vector3(.07f, .82f, .1f));
            }
            Box(Trim, new Vector3(x + pair * 2.95f, 2.55f, .0f), new Vector3(.14f, .16f, 12.7f));
            Beam(Frame, new Vector3(x + pair * 1.2f, 6.38f, -5.85f),
                new Vector3(x + pair * 1.2f, 6.38f, 6.15f), .095f);
            Equipment(new Vector3(x, 6.52f, -3.6f), 1.18f, 1.58f);
            Equipment(new Vector3(x, 6.52f, 3.25f), 1.18f, 1.58f);
        }

        Axial(Frame, Vector3.up * 2.86f, Vector3.right, 18, (-5.8f, 1.78f), (5.8f, 1.78f));
        Axial(Dark, Vector3.up * 2.86f, Vector3.right, 18, (-3.2f, 1.86f), (-2.83f, 1.86f));
        Axial(Dark, Vector3.up * 2.86f, Vector3.right, 18, (2.83f, 1.86f), (3.2f, 1.86f));
        Axial(Shell, new Vector3(0f, 2.9f, 2.3f), Vector3.forward, 16,
            (-2.75f, 1.9f), (-2.01f, 2.1f), (5.6f, 2.1f), (6.1f, 1.9f));
        Box(Frame, new Vector3(0f, -1.04f, 7.22f), new Vector3(4.15f, 3.19f, 6.94f));
        Airlock(new Vector3(0f, 2.36f, 7.98f), Vector3.forward, 4.36f, 1.59f);
        Airlock(new Vector3(0f, 2.36f, -1.9f), Vector3.back, 3.8f, 1.42f);
        Box(Dark, new Vector3(0f, -1.05f, 12.55f), new Vector3(3.15f, 3.2f, 2.05f));
        Box(Dark, new Vector3(0f, .42f, 12.96f), new Vector3(3.35f, .34f, 1.8f));

        Equipment(new Vector3(-2.65f, .5f, -8.2f), 1.9f, 2.65f);
        for (int lane = -1; lane <= 1; lane += 2)
        {
            Axial(Frame, new Vector3(lane * 1.0f, 1.92f, -8f), Vector3.right, 10,
                (Mathf.Min(0, lane * 3.5f), .19f), (Mathf.Max(0, lane * 3.5f), .19f));
            Beam(Trim, new Vector3(lane * 1.33f, 1.15f, -6.7f),
                new Vector3(lane * 3.8f, 1.15f, -6.7f), .13f);
            Beam(Dark, new Vector3(lane * 3.8f, 1.15f, -6.7f),
                new Vector3(lane * 4.6f, 2.35f, -6.7f), .19f);
        }
    }

    void Airlock(Vector3 start, Vector3 direction, float length, float radius)
    {
        Axial(Frame, start, direction, 12, (0f, radius * 1.04f), (.32f, radius),
            (length - .52f, radius), (length - .13f, radius * 1.09f));
        Axial(Dark, start, direction, 12, (length - .16f, radius * .92f), (length, radius * .92f));
        Axial(Shell, start, direction, 12, (length - .03f, radius * .59f), (length + .035f, radius * .59f));
        Quaternion facing = Quaternion.LookRotation(direction);
        Box(Trim, start + direction * (length + .069f) + Vector3.down * .19f,
            new Vector3(.12f, 1.36f, .067f), facing);
        Box(Beacon, start + direction * (length + .06f) + Vector3.up * (radius * .77f),
            new Vector3(.32f, .10f, .095f), facing);
    }

    void Equipment(Vector3 basePoint, float width, float length)
    {
        Box(Dark, basePoint + Vector3.up * .13f, new Vector3(width + .14f, .26f, length + .15f));
        Box(Frame, basePoint + Vector3.up * .69f, new Vector3(width, 1.03f, length));
        Box(Shell, basePoint + Vector3.up * 1.25f, new Vector3(width + .07f, .13f, length + .07f));
        for (int vent = 0; vent < 5; vent++)
            Box(Dark, basePoint + new Vector3(0f, .38f + vent * .14f, length * .5f + .019f),
                new Vector3(width * .75f, .058f, .061f));
        Box(Trim, basePoint + new Vector3(width * .29f, 1.33f, -.18f), new Vector3(.24f, .026f, .68f));
    }

    void Radar(Vector3 center)
    {
        Beam(Frame, center + Vector3.down * 2.35f, center, .42f);
        Quaternion turn = Quaternion.Euler(-38f, -21f, 0f);
        Vector3 Dish(float radius, int segment)
        {
            float angle = segment * Mathf.PI / 12f;
            return center + turn * new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), .20f * radius * radius);
        }
        for (int segment = 0; segment < 24; segment++)
        {
            Vector3 a = Dish(.75f, segment), b = Dish(1.52f, segment);
            Vector3 c = Dish(1.52f, segment + 1), d = Dish(.75f, segment + 1);
            Triangle(Shell, center, a, d);
            Quad(Frame, a, b, c, d);
            Triangle(Dark, center, d, a);
            Quad(Dark, d, c, b, a);
        }
        Vector3 receiver = center + turn * Vector3.forward * 1.45f;
        for (int arm = 0; arm < 3; arm++) Beam(Frame, Dish(1.44f, arm * 8), receiver, .078f);
        Box(Dark, receiver, new Vector3(.28f, .3f, .34f), turn);
    }

    Material Surface(string name, Color color, float metallic, float smoothness)
    {
        var material = new Material(urpLitTemplate) { name = name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        return material;
    }

    void Axial(int material, Vector3 center, Vector3 axis, int sides, params (float offset, float radius)[] rings)
    {
        var rotation = Quaternion.FromToRotation(Vector3.up, axis);
        Vector3 Point(int ring, int index)
        {
            float angle = (index + .5f) * Mathf.PI * 2f / sides;
            var section = rings[ring];
            return center + rotation * new Vector3(Mathf.Cos(angle) * section.radius, section.offset,
                Mathf.Sin(angle) * section.radius);
        }
        int last = rings.Length - 1;
        for (int i = 0; i < sides; i++)
        {
            for (int ring = 0; ring < last; ring++)
                Quad(material, Point(ring, i), Point(ring + 1, i), Point(ring + 1, i + 1), Point(ring, i + 1));
            Triangle(material, center + axis * rings[0].offset, Point(0, i), Point(0, i + 1));
            Triangle(material, center + axis * rings[last].offset, Point(last, i + 1), Point(last, i));
        }
    }

    void Beam(int material, Vector3 start, Vector3 end, float width)
    {
        Vector3 delta = end - start;
        Box(material, (start + end) * .5f, new Vector3(width, delta.magnitude, width),
            Quaternion.FromToRotation(Vector3.up, delta));
    }

    void Box(int material, Vector3 center, Vector3 size) => Box(material, center, size, Quaternion.identity);

    void Box(int material, Vector3 center, Vector3 size, Quaternion rotation)
    {
        Vector3 P(float x, float y, float z) => center + rotation * Vector3.Scale(size, new Vector3(x, y, z)) * .5f;
        Vector3 a = P(-1, 1, -1), b = P(-1, 1, 1), c = P(1, 1, 1), d = P(1, 1, -1);
        Vector3 e = P(-1, -1, -1), f = P(-1, -1, 1), g = P(1, -1, 1), h = P(1, -1, -1);
        Quad(material, a, b, c, d);
        Quad(material, b, a, e, f);
        Quad(material, c, b, f, g);
        Quad(material, d, c, g, h);
        Quad(material, a, d, h, e);
        Quad(material, e, h, g, f);
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
        foreach (Material material in materials) Destroy(material);
    }
}
