using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider))]
public sealed class LunarBaseModule : MonoBehaviour
{
    [SerializeField] BuildingKind kind;
    [SerializeField] Material urpLitTemplate;
    [SerializeField] Material glazing;
    [SerializeField] Transform[] connectionPorts;

    public IReadOnlyList<Transform> ConnectionPorts => connectionPorts;

    const int Shell = 0;
    const int Frame = 1;
    const int Dark = 2;
    const int Trim = 3;
    const int Window = 4;
    const int Beacon = 5;
    const int Interior = 6;
    const int Seat = 7;

    readonly List<Vector3> vertices = new List<Vector3>();
    readonly List<int>[] faces = {
        new List<int>(), new List<int>(), new List<int>(), new List<int>(),
        new List<int>(), new List<int>(), new List<int>(), new List<int>()
    };
    readonly List<Vector3> glassVertices = new List<Vector3>();
    Mesh mesh;
    Mesh glassMesh;
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
            Surface("Approach beacons", new Color(.14f, .8f, .65f), .05f, .48f),
            Surface("Interior wall and floor lining", habitat ? new Color(.68f, .55f, .45f) : new Color(.49f, .57f, .63f), .09f, .48f),
            Surface("Padded seats and bedding", habitat ? new Color(.28f, .40f, .42f) : new Color(.14f, .27f, .34f), .02f, .29f)
        };
        materials[Window].EnableKeyword("_EMISSION");
        materials[Window].SetColor("_EmissionColor",
            habitat ? new Color(.83f, .37f, .09f) * 1.14f : new Color(.09f, .49f, .77f) * 1.28f);
        materials[Beacon].EnableKeyword("_EMISSION");
        materials[Beacon].SetColor("_EmissionColor", new Color(.05f, .83f, .69f) * 1.6f);
        GetComponent<MeshFilter>().sharedMesh = mesh;
        GetComponent<MeshRenderer>().sharedMaterials = materials;
        materials[Interior].EnableKeyword("_EMISSION");
        materials[Interior].SetColor("_EmissionColor",
            habitat ? new Color(.15f, .095f, .055f) : new Color(.077f, .125f, .16f));

        glassMesh = new Mesh { name = "Cabin and module glazing" };
        glassMesh.SetVertices(glassVertices);
        var triangles = new int[glassVertices.Count];
        for (int i = 0; i < triangles.Length; i++) triangles[i] = i;
        glassMesh.SetTriangles(triangles, 0);
        glassMesh.RecalculateNormals();
        glassMesh.RecalculateBounds();
        glassVertices.Clear();
        var windows = new GameObject("Transparent pressure glazing", typeof(MeshFilter), typeof(MeshRenderer));
        windows.transform.SetParent(transform, false);
        windows.GetComponent<MeshFilter>().sharedMesh = glassMesh;
        var renderer = windows.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = glazing;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    void BuildCommand()
    {
        const float floor = 20.38f;
        Axial(Dark, Vector3.zero, Vector3.up, 8, (-2.65f, 10.65f), (.56f, 11.3f), (1.34f, 10.58f));
        Axial(Shell, Vector3.zero, Vector3.up, 8,
            (1.25f, 10.45f), (4.3f, 9.8f), (15.7f, 6.15f), (18.7f, 6.15f));
        Axial(Frame, Vector3.zero, Vector3.up, 8, (15.3f, 6.23f), (16.1f, 6.45f));
        Axial(Frame, Vector3.zero, Vector3.up, 8,
            (17.5f, 6.27f), (18.35f, 7.72f), (19.5f, 11.88f), (20.2f, 12.48f));
        Axial(Dark, Vector3.zero, Vector3.up, 8, (20.14f, 12.48f), (20.33f, 12.38f));
        Axial(Interior, Vector3.zero, Vector3.up, 8, (20.31f, 12.19f), (floor, 12.19f));
        Axial(Dark, Vector3.zero, Vector3.up, 8, (25.60f, 13.18f), (26.02f, 13.18f));
        Axial(Shell, Vector3.zero, Vector3.up, 8, (26.02f, 13.08f), (26.75f, 11.05f), (27f, 10.34f));

        for (int side = 0; side < 8; side++)
        {
            Quaternion turn = Quaternion.Euler(0f, side * 45f, 0f);
            Vector3 P(float x, float y, float z) => turn * new Vector3(x, y, z);
            const float bottom = 4.70f, top = 5.00f;
            Pane(P(-bottom, 20.41f, 11.35f), P(bottom, 20.41f, 11.35f),
                P(top, 25.64f, 12.06f), P(-top, 25.64f, 12.06f));
            for (int pillar = -2; pillar <= 2; pillar++)
                Beam(Frame, P(pillar * bottom * .5f, 20.36f, 11.37f),
                    P(pillar * top * .5f, 25.7f, 12.08f), Mathf.Abs(pillar) == 2 ? .34f : .16f);
            Beam(Frame, P(-bottom, 20.40f, 11.38f), P(bottom, 20.40f, 11.38f), .24f);
            Beam(Frame, P(-top, 25.59f, 12.12f), P(top, 25.59f, 12.12f), .24f);
            Workstation(P(-2.55f, floor, 9.38f), turn);
            Workstation(P(2.55f, floor, 9.38f), turn);
            Box(Window, P(0f, 25.37f, 8.08f), new Vector3(3.95f, .10f, .21f), turn);

            // Tapered columns run from the technical base to the cabin's supporting ring.
            for (int edge = -1; edge <= 1; edge += 2)
            {
                Beam(Frame, P(edge * 3.3f, 1.03f, 9.91f), P(edge * 2.43f, 16.45f, 5.91f), .46f);
                Beam(Dark, P(edge * 2.43f, 16.45f, 5.88f), P(edge * 3.18f, 19.78f, 10.66f), .42f);
                Box(Trim, P(edge * 3.65f, 1.8f, 10.06f), new Vector3(.31f, .68f, .18f), turn);
            }
            Box(Dark, P(0f, 3.07f, 9.33f), new Vector3(3.88f, 1.46f, .7f), turn);
            for (int vent = 0; vent < 6; vent++)
                Box(Frame, P(0f, 2.53f + vent * .18f, 9.71f), new Vector3(3.21f, .065f, .09f), turn);
            Beam(Trim, P(-.56f, 6.62f, 8.38f), P(-.56f, 13.66f, 6.42f), .118f);
            Beam(Frame, P(0f, 6.62f, 8.38f), P(0f, 14.18f, 6.31f), .115f);
        }

        Axial(Frame, Vector3.up * floor, Vector3.up, 8, (0f, 2.22f), (.71f, 2.74f));
        Axial(Dark, Vector3.up * (floor + .71f), Vector3.up, 12, (0f, 3.05f), (.25f, 3.05f));
        Axial(Window, Vector3.up * (floor + .96f), Vector3.up, 12, (0f, 2.2f), (.023f, 2.2f));
        InteriorLight(new Vector3(0f, 23.2f, 0f), new Color(.63f, .82f, 1f), 14f, 30f);

        Entry(9.5f, 13.84f, 2.16f);
        for (int side = -1; side <= 1; side += 2)
        {
            Box(Frame, new Vector3(side * 9.76f, 2.19f, 0f), new Vector3(.44f, 2.55f, 2.36f));
            Box(Dark, new Vector3(side * 10.01f, 2.19f, 0f), new Vector3(.13f, 1.83f, 1.73f));
            Box(Beacon, new Vector3(side * 10.1f, 3.42f, 0f), new Vector3(.08f, .11f, .61f));
        }

        Equipment(new Vector3(-5.52f, 27f, -4.17f), 1.37f, 1.6f);
        Equipment(new Vector3(-2.92f, 27f, -4.83f), 1.5f, 1.75f);
        Vector3 mast = new Vector3(-4.35f, 27f, -3.01f);
        Beam(Frame, mast, mast + Vector3.up * 4.55f, .19f);
        for (int arm = 0; arm < 3; arm++)
        {
            Vector3 bar = mast + Vector3.up * (2.58f + arm * .62f);
            Beam(Frame, bar + Vector3.left * (1.23f - arm * .21f),
                bar + Vector3.right * (1.23f - arm * .21f), .10f);
        }
        Box(Beacon, mast + Vector3.up * 4.57f, new Vector3(.24f, .29f, .24f));
        Radar(new Vector3(4.79f, 29.35f, -3.15f));
        for (int side = -1; side <= 1; side += 2)
            Box(Beacon, new Vector3(side * 8.04f, 26.81f, 5.27f), new Vector3(.38f, .24f, .38f));
    }

    void BuildHabitat()
    {
        Vector2[] footprint =
        {
            new Vector2(-8.45f, -8.20f), new Vector2(8.45f, -8.20f),
            new Vector2(10.85f, -5.80f), new Vector2(10.85f, 5.95f),
            new Vector2(8.53f, 8.27f), new Vector2(-8.53f, 8.27f),
            new Vector2(-10.85f, 5.95f), new Vector2(-10.85f, -5.80f)
        };

        Vector3 P(Vector2 point, float y) => new Vector3(point.x, y, point.y);
        void Wall(int skin, Vector2 a, Vector2 b, float lo, float hi)
        {
            Quad(skin, P(a, lo), P(a, hi), P(b, hi), P(b, lo));
            if (lo >= .80f)
                Quad(Interior, P(b * .993f, lo), P(b * .993f, hi), P(a * .993f, hi), P(a * .993f, lo));
        }

        for (int side = 0; side < footprint.Length; side++)
        {
            Vector2 a = footprint[side];
            Vector2 b = footprint[(side + 1) % footprint.Length];
            Wall(Dark, a * .91f, b * .91f, -2.64f, .50f);
            Quad(Frame, P(a * .91f, .50f), P(a, .82f), P(b, .82f), P(b * .91f, .50f));
            Quad(Shell, P(a, 8.48f), P(a * .92f, 8.94f), P(b * .92f, 8.94f), P(b, 8.48f));
            if (side % 2 != 0)
            {
                Wall(Shell, a, b, .81f, 8.5f);
                Beam(Frame, P((a + b) * .503f, 1.1f), P((a + b) * .503f, 8.13f), .18f);
                continue;
            }

            // Two glazed levels in one uninterrupted pressure hull.
            Wall(Shell, a, b, .81f, 1.48f);
            Wall(Frame, a, b, 3.86f, 5.13f);
            Wall(Shell, a, b, 7.93f, 8.48f);
            for (int level = 0; level < 2; level++)
            {
                float sill = level == 0 ? 1.48f : 5.13f;
                float header = level == 0 ? 3.86f : 7.93f;
                Pane(P(b, sill), P(a, sill), P(a, header), P(b, header));
                Beam(Frame, P(a, sill), P(b, sill), .24f);
                Beam(Frame, P(a, header), P(b, header), .27f);
                for (int pillar = 0; pillar <= 5; pillar++)
                {
                    Vector2 center = Vector2.Lerp(a, b, pillar / 5f);
                    Beam(Frame, P(center, sill), P(center, header), pillar % 5 == 0 ? .28f : .14f);
                }
            }
        }

        var floor = new List<Vector2>();
        var roof = new List<Vector2>();
        foreach (Vector2 point in footprint)
        {
            floor.Add(point * .993f);
            roof.Add(point * .92f);
        }
        FloorPlate(floor, .86f, Interior);
        FloorPlate(roof, 8.94f, Shell);
        FloorPlate(roof, 8.48f, Interior, true);

        // Split the upper deck around the open stairwell.
        List<Vector2> middle = ClipFloor(ClipFloor(floor, true, -1.47f, false), true, 1.47f, true);
        Mezzanine(ClipFloor(floor, true, -1.47f, true));
        Mezzanine(ClipFloor(floor, true, 1.47f, false));
        Mezzanine(ClipFloor(middle, false, -3.59f, true));
        Mezzanine(ClipFloor(middle, false, 2.35f, false));
        void Mezzanine(List<Vector2> section)
        {
            FloorPlate(section, 4.52f, Interior);
            FloorPlate(section, 4.33f, Interior, true);
        }

        for (int step = 0; step < 17; step++)
        {
            float height = .86f + (step + 1) * (3.66f / 17f);
            Box(step % 4 == 0 ? Frame : Dark, new Vector3(0f, height - .10f, -3.37f + step * .348f),
                new Vector3(2.18f, .20f, .37f));
        }
        for (int rail = -1; rail <= 1; rail += 2)
        {
            Beam(Frame, new Vector3(rail * 1.1f, 1.9f, -3.38f), new Vector3(rail * 1.1f, 5.49f, 2.19f), .12f);
            for (int post = 0; post < 4; post++)
            {
                float t = post / 3f;
                float y = 4.52f - (1f - t) * 3.66f;
                float z = Mathf.Lerp(-3.38f, 2.19f, t);
                Beam(Trim, new Vector3(rail * 1.1f, y, z), new Vector3(rail * 1.1f, y + .96f, z), .11f);
            }
        }

        for (int side = -1; side <= 1; side += 2)
        {
            Quaternion facing = Quaternion.Euler(0f, side * 90f, 0f);
            Bunk(new Vector3(side * 8.15f, 4.53f, -4.08f), facing);
            Bunk(new Vector3(side * 8.15f, 4.53f, 3.55f), facing);
            Box(Frame, new Vector3(side * 7.7f, 5.54f, -6.5f), new Vector3(2.74f, 2.02f, .65f));
            for (int door = -1; door <= 1; door++)
                Box(Trim, new Vector3(side * 7.7f + door * .72f, 5.55f, -6.16f), new Vector3(.08f, .52f, .044f));
            Box(Interior, new Vector3(side * 7.61f, 1.59f, .96f), new Vector3(1.7f, .18f, 4.14f));
            Box(Frame, new Vector3(side * 7.61f, 1.18f, .96f), new Vector3(.51f, .66f, 1.3f));
            Chair(new Vector3(side * 6.12f, .86f, .05f), facing);
            Chair(new Vector3(side * 6.12f, .86f, 2.03f), facing);
            Box(Window, new Vector3(side * 6.9f, 4.31f, -1.78f), new Vector3(.29f, .096f, 6.92f));
            Box(Window, new Vector3(side * 7.22f, 8.29f, .27f), new Vector3(.31f, .094f, 8.94f));
            InteriorLight(new Vector3(side * 6.4f, 7.2f, .3f), new Color(1f, .72f, .44f), 8.3f, 16f);
        }

        Box(Seat, new Vector3(0f, 1.33f, -6.56f), new Vector3(5.03f, .78f, 1.07f));
        Box(Seat, new Vector3(0f, 1.98f, -6.94f), new Vector3(5.03f, .9f, .37f));
        Box(Interior, new Vector3(0f, 1.26f, -4.98f), new Vector3(2.84f, .35f, .75f));
        Box(Interior, new Vector3(0f, 1.66f, 5.11f), new Vector3(3.27f, .17f, 1.53f));
        Chair(new Vector3(-1.15f, .86f, 3.66f), Quaternion.identity);
        Chair(new Vector3(1.15f, .86f, 6.36f), Quaternion.Euler(0f, 180f, 0f));
        InteriorLight(new Vector3(0f, 3.46f, -1.47f), new Color(1f, .71f, .43f), 11.8f, 24f);

        Box(Dark, new Vector3(0f, 9.13f, -1.52f), new Vector3(13.02f, .43f, 5.15f));
        Box(Frame, new Vector3(0f, 9.61f, -1.76f), new Vector3(10.3f, .57f, 3.57f));
        for (int vent = -2; vent <= 2; vent++)
        {
            Box(Dark, new Vector3(vent * 1.83f, 9.64f, .054f), new Vector3(1.2f, .24f, .09f));
            Box(Trim, new Vector3(vent * 1.83f, 9.918f, -1.9f), new Vector3(.10f, .027f, .81f));
        }
        Equipment(new Vector3(-6.98f, 8.95f, -4.76f), 1.75f, 1.54f);
        Equipment(new Vector3(6.84f, 8.95f, -4.76f), 1.75f, 1.54f);

        Entry(7.81f, 12.26f, 2.12f);
        // A flush service-panel socket retains the future connection point.
        Box(Frame, new Vector3(0f, 2.34f, -8.34f), new Vector3(2.15f, 2.45f, .37f));
        Box(Dark, new Vector3(0f, 2.34f, -8.55f), new Vector3(1.61f, 1.8f, .085f));
    }

    void Entry(float start, float end, float halfWidth)
    {
        float mid = (start + end) * .5f;
        float depth = end - start;
        Box(Dark, new Vector3(0f, -1.04f, mid), new Vector3(halfWidth * 1.98f, 3.2f, depth + .42f));
        Box(Frame, new Vector3(0f, .65f, mid), new Vector3(halfWidth * 2.12f, .31f, depth + .49f));
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * (halfWidth - .14f);
            Box(Shell, new Vector3(x, 2.14f, mid), new Vector3(.35f, 2.87f, depth));
            Beam(Frame, new Vector3(x, 3.52f, start), new Vector3(x, 3.52f, end), .18f);
            Box(Trim, new Vector3(side * 1.76f, .826f, end - .08f), new Vector3(.35f, .04f, .35f));
        }
        Box(Shell, new Vector3(0f, 3.61f, mid), new Vector3(halfWidth * 2.04f, .30f, depth + .11f));
        Box(Dark, new Vector3(0f, 2.09f, end - .02f), new Vector3(3.27f, 2.69f, .22f));
        Box(Frame, new Vector3(0f, 2.08f, end + .105f), new Vector3(2.34f, 2.34f, .10f));
        Box(Dark, new Vector3(0f, 2.08f, end + .161f), new Vector3(.043f, 2.12f, .03f));
        Box(Beacon, new Vector3(0f, 3.52f, end + .05f), new Vector3(.79f, .108f, .15f));
    }

    void FloorPlate(List<Vector2> points, float height, int material, bool under = false)
    {
        for (int i = 1; i + 1 < points.Count; i++)
        {
            Vector3 P(int j) => new Vector3(points[j].x, height, points[j].y);
            if (under) Triangle(material, P(0), P(i), P(i + 1));
            else Triangle(material, P(0), P(i + 1), P(i));
        }
    }

    static List<Vector2> ClipFloor(List<Vector2> polygon, bool xAxis, float boundary, bool keepBelow)
    {
        var result = new List<Vector2>();
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
            float sa = (xAxis ? a.x : a.y) - boundary;
            float sb = (xAxis ? b.x : b.y) - boundary;
            bool aIn = keepBelow ? sa <= 0f : sa >= 0f;
            bool bIn = keepBelow ? sb <= 0f : sb >= 0f;
            if (aIn) result.Add(a);
            if (aIn != bIn) result.Add(Vector2.Lerp(a, b, sa / (sa - sb)));
        }
        return result;
    }

    void Workstation(Vector3 position, Quaternion turn)
    {
        Vector3 P(float x, float y, float z) => position + turn * new Vector3(x, y, z);
        Box(Dark, P(0f, .46f, 0f), new Vector3(1.74f, .91f, .28f), turn);
        Box(Interior, P(0f, .91f, 0f), new Vector3(2.09f, .14f, 1.03f), turn);
        for (int screen = -1; screen <= 1; screen++)
        {
            Box(Frame, P(screen * .59f, 1.22f, .25f), new Vector3(.54f, .51f, .11f), turn);
            Box(Window, P(screen * .59f, 1.24f, .184f), new Vector3(.43f, .36f, .035f), turn);
            Box(Beacon, P(screen * .59f, 1.027f, -.2f), new Vector3(.22f, .025f, .105f), turn);
        }
        Chair(P(0f, 0f, -1.14f), turn);
    }

    void Bunk(Vector3 position, Quaternion turn)
    {
        Vector3 P(float x, float y, float z) => position + turn * new Vector3(x, y, z);
        for (int level = 0; level <= 1; level++)
        {
            float y = .28f + level * 1.54f;
            Box(Frame, P(0f, y, 0f), new Vector3(1.22f, .21f, 2.2f), turn);
            Box(Seat, P(0f, y + .18f, 0f), new Vector3(1.05f, .20f, 1.99f), turn);
            Box(Shell, P(0f, y + .32f, -.72f), new Vector3(.82f, .15f, .46f), turn);
        }
        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
            Box(Frame, P(x * .57f, 1.16f, z * 1.01f), new Vector3(.10f, 2.31f, .1f), turn);
        for (int step = 0; step < 5; step++)
            Box(Frame, P(.63f, .31f + step * .32f, .40f), new Vector3(.07f, .06f, .48f), turn);
    }

    void Chair(Vector3 basePoint, Quaternion turn)
    {
        Vector3 P(float x, float y, float z) => basePoint + turn * new Vector3(x, y, z);
        Box(Frame, P(0f, .24f, 0f), new Vector3(.19f, .48f, .23f), turn);
        Box(Frame, P(0f, .04f, 0f), new Vector3(.67f, .08f, .65f), turn);
        Box(Seat, P(0f, .52f, 0f), new Vector3(.66f, .17f, .59f), turn);
        Box(Seat, P(0f, .88f, -.27f), new Vector3(.62f, .7f, .16f), turn);
        for (int arm = -1; arm <= 1; arm += 2)
            Box(Frame, P(arm * .35f, .69f, -.05f), new Vector3(.08f, .14f, .35f), turn);
    }

    void InteriorLight(Vector3 position, Color color, float range, float intensity)
    {
        var fixture = new GameObject("Interior lighting", typeof(Light));
        fixture.transform.SetParent(transform, false);
        fixture.transform.localPosition = position;
        var light = fixture.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
    }

    void Pane(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        glassVertices.Add(a);
        glassVertices.Add(b);
        glassVertices.Add(c);
        glassVertices.Add(a);
        glassVertices.Add(c);
        glassVertices.Add(d);
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
        if (glassMesh != null) Destroy(glassMesh);
        if (mesh != null) Destroy(mesh);
        if (materials == null) return;
        foreach (Material material in materials) Destroy(material);
    }
}
