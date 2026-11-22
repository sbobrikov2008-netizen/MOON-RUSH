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
        Axial(Dark, Vector3.zero, Vector3.up, 8, (-2.65f, 10.9f), (.45f, 11.3f), (1.06f, 10.8f));
        Axial(Shell, Vector3.zero, Vector3.up, 8,
            (1.06f, 10.55f), (2f, 10.8f), (6.9f, 8.75f), (8.5f, 8.9f));
        Axial(Frame, Vector3.zero, Vector3.up, 8, (7.95f, 8.95f), (8.43f, 9.5f), (9.02f, 12.45f));
        Axial(Dark, Vector3.zero, Vector3.up, 8, (8.98f, 12.45f), (9.42f, 12.34f));
        Axial(Interior, Vector3.zero, Vector3.up, 8, (9.40f, 11.98f), (9.46f, 11.98f));
        Axial(Dark, Vector3.zero, Vector3.up, 8, (13.55f, 13.08f), (13.94f, 13.08f));
        Axial(Shell, Vector3.zero, Vector3.up, 8, (13.94f, 12.92f), (14.54f, 11.35f), (14.71f, 10.2f));

        for (int side = 0; side < 8; side++)
        {
            Quaternion turn = Quaternion.Euler(0f, side * 45f, 0f);
            Vector3 P(float x, float y, float z) => turn * new Vector3(x, y, z);
            const float bottom = 4.68f, top = 4.92f;
            Pane(P(-bottom, 9.48f, 11.3f), P(bottom, 9.48f, 11.3f),
                P(top, 13.56f, 11.88f), P(-top, 13.56f, 11.88f));
            Beam(Frame, P(-bottom, 9.45f, 11.31f), P(bottom, 9.45f, 11.31f), .22f);
            Beam(Frame, P(-top, 13.52f, 11.89f), P(top, 13.52f, 11.89f), .22f);
            for (int pillar = -2; pillar <= 2; pillar++)
                Beam(Frame, P(pillar * (bottom * .5f), 9.45f, 11.36f),
                    P(pillar * (top * .5f), 13.6f, 11.94f), Mathf.Abs(pillar) == 2 ? .32f : .14f);

            // Consoles sit well inside the hull and face out toward any landing pad.
            Workstation(P(-2.55f, 9.47f, 9.12f), turn);
            Workstation(P(2.55f, 9.47f, 9.12f), turn);
            Box(Window, P(0f, 13.33f, 8.42f), new Vector3(4.15f, .10f, .18f), turn);
            Beam(Frame, P(-3.5f, 8.82f, 10.45f), P(-3.2f, 5.68f, 8.81f), .33f);
            Beam(Frame, P(3.5f, 8.82f, 10.45f), P(3.2f, 5.68f, 8.81f), .33f);
            Box(Dark, P(0f, 3.37f, 9.57f), new Vector3(4.0f, 1.74f, .67f), turn);
            for (int vent = 0; vent < 6; vent++)
                Box(Frame, P(0f, 2.77f + vent * .21f, 9.93f), new Vector3(3.2f, .07f, .09f), turn);
            Box(Trim, P(3.42f, 1.56f, 9.8f), new Vector3(.32f, .51f, .18f), turn);
            Box(Beacon, P(-3.42f, 1.78f, 9.81f), new Vector3(.28f, .15f, .18f), turn);
        }

        Axial(Frame, new Vector3(0f, 9.45f, 0f), Vector3.up, 8, (0f, 2.25f), (.71f, 2.78f));
        Axial(Dark, new Vector3(0f, 10.10f, 0f), Vector3.up, 12, (0f, 3.1f), (.24f, 3.1f));
        Axial(Window, new Vector3(0f, 10.33f, 0f), Vector3.up, 12, (0f, 2.28f), (.025f, 2.28f));
        Box(Interior, new Vector3(0f, 13.44f, 0f), new Vector3(7.5f, .10f, .45f));
        Box(Window, new Vector3(0f, 13.365f, 0f), new Vector3(6.95f, .048f, .24f));
        InteriorLight(new Vector3(0f, 11.95f, 0f), new Color(.58f, .8f, 1f), 13f, 26f);

        Airlock(new Vector3(0f, 2.34f, 10.2f), Vector3.forward, 4.1f, 1.73f);
        Airlock(new Vector3(10.2f, 2.34f, 0f), Vector3.right, 3.7f, 1.36f);
        Airlock(new Vector3(-10.2f, 2.34f, 0f), Vector3.left, 3.7f, 1.36f);
        Box(Frame, new Vector3(0f, -1.01f, 12.68f), new Vector3(3.7f, 3.16f, 4.12f));
        Box(Dark, new Vector3(0f, .61f, 14.85f), new Vector3(3.7f, .3f, 1.94f));
        for (int step = -2; step <= 2; step++)
            Box(Trim, new Vector3(step * .82f, .778f, 15.42f), new Vector3(.37f, .035f, .27f));

        for (int i = 0; i < 3; i++)
            Equipment(new Vector3(-6.65f + i * 2.46f, 14.72f, -4.84f), 1.46f, 1.5f);
        Vector3 mast = new Vector3(-4.95f, 14.72f, -3.15f);
        Beam(Frame, mast, mast + Vector3.up * 5.25f, .19f);
        for (int leg = 0; leg < 4; leg++)
        {
            float angle = leg * Mathf.PI / 2f;
            Beam(Frame, mast + new Vector3(Mathf.Cos(angle), .12f, Mathf.Sin(angle)),
                mast + Vector3.up * 2.25f, .13f);
        }
        for (int arm = 0; arm < 3; arm++)
        {
            Vector3 bar = mast + Vector3.up * (3.1f + arm * .63f);
            Beam(Frame, bar + Vector3.left * (1.38f - arm * .26f),
                bar + Vector3.right * (1.38f - arm * .26f), .10f);
        }
        Box(Beacon, mast + Vector3.up * 5.23f, new Vector3(.23f, .32f, .23f));
        Radar(new Vector3(5.15f, 17.08f, -3.0f));
        for (int i = -1; i <= 1; i += 2)
            Box(Beacon, new Vector3(i * 8.1f, 14.35f, 5.75f), new Vector3(.34f, .22f, .35f));
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

    void BuildHabitat()
    {
        Vector3 lounge = new Vector3(0f, 0f, .4f);
        HabitatRoom(lounge, 3.15f, 7.42f, 1.28f, 6.33f, (1 << 0) | (1 << 4), (1 << 2) | (1 << 6));
        HabitatRoom(new Vector3(-7.3f, 0f, .4f), 3.4f, 6.4f, 1.14f, 5.43f,
            (1 << 4) | (1 << 6), 1 << 2);
        HabitatRoom(new Vector3(7.35f, 0f, .4f), 3.65f, 5.95f, 1.75f, 5.02f,
            (1 << 2) | (1 << 4), 1 << 6);
        // Both links have open ends aligned to openings in the inhabited modules.
        PressureLink(-3.59f);
        PressureLink(3.53f);

        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 7.0f;
            Quaternion facing = Quaternion.Euler(0f, -side * 90f, 0f);
            Bunk(new Vector3(x, .86f, -3.47f), facing);
            Bunk(new Vector3(x, .86f, 3.55f), facing);
            Box(Interior, new Vector3(side * 9.05f, 1.36f, -.23f), new Vector3(1.49f, .96f, 1.3f));
            Box(Shell, new Vector3(side * 9.05f, 1.87f, -.23f), new Vector3(1.57f, .13f, 1.5f));
            Chair(new Vector3(side * 8.01f, .86f, -.23f), Quaternion.Euler(0f, side * 90f, 0f));
            Box(Frame, new Vector3(side * 6.42f, 2.23f, 5.23f), new Vector3(2.03f, 2.73f, .69f));
            for (int door = -1; door <= 1; door++)
                Box(Trim, new Vector3(side * 6.42f + door * .59f, 2.15f, 4.87f), new Vector3(.07f, .58f, .04f));
            Box(Window, new Vector3(side * 7.82f, (side < 0 ? 5.43f : 5.02f) - .42f, .4f),
                new Vector3(.28f, .105f, 7.15f));
            InteriorLight(new Vector3(side * 7.55f, 3.76f, .4f), new Color(1f, .69f, .40f), 7f, 13f);
            float roof = side < 0 ? 5.43f : 5.02f;
            Equipment(new Vector3(side * 7.22f, roof, -2.95f), 1.53f, 2.06f);
            Beam(Frame, new Vector3(side * 8.6f, roof + .19f, -5.13f),
                new Vector3(side * 8.6f, roof + .19f, 4.1f), .13f);
        }

        // A separate higher common room holds the dining surface and lounge seating.
        Box(Interior, new Vector3(0f, 1.60f, 3.51f), new Vector3(2.1f, .16f, 2.8f));
        Box(Frame, new Vector3(0f, 1.17f, 3.51f), new Vector3(.45f, .73f, 1.35f));
        Chair(new Vector3(-1.72f, .86f, 3.06f), Quaternion.Euler(0f, 90f, 0f));
        Chair(new Vector3(1.72f, .86f, 3.96f), Quaternion.Euler(0f, -90f, 0f));
        Box(Seat, new Vector3(0f, 1.36f, -3.46f), new Vector3(4.71f, .79f, 1.03f));
        Box(Seat, new Vector3(0f, 1.98f, -3.84f), new Vector3(4.71f, .85f, .35f));
        Box(Interior, new Vector3(0f, 1.19f, -2.03f), new Vector3(1.9f, .3f, .65f));
        Box(Frame, new Vector3(-2.49f, 2.5f, -4.35f), new Vector3(.58f, 3.21f, 1.42f));
        Box(Window, new Vector3(0f, 5.88f, 1.26f), new Vector3(3.47f, .104f, .31f));
        InteriorLight(new Vector3(0f, 4.76f, 1.2f), new Color(1f, .74f, .48f), 8f, 16f);

        Axial(Shell, new Vector3(0f, 2.36f, 7.12f), Vector3.forward, 16,
            (0f, 1.86f), (1.18f, 1.86f));
        Airlock(new Vector3(0f, 2.36f, 7.98f), Vector3.forward, 4.36f, 1.59f);
        Airlock(new Vector3(0f, 2.36f, -6.6f), Vector3.back, 3.1f, 1.42f);
        Box(Dark, new Vector3(0f, -1.05f, 12.55f), new Vector3(3.15f, 3.2f, 2.05f));
        Box(Frame, new Vector3(0f, .42f, 12.96f), new Vector3(3.35f, .34f, 1.8f));
        Box(Dark, new Vector3(-2.75f, -1.02f, -8.58f), new Vector3(2.02f, 3.2f, 2.97f));
        Equipment(new Vector3(-2.75f, .60f, -8.58f), 1.93f, 2.82f);
        for (int line = 0; line < 3; line++)
        {
            float x = -2.34f - line * .29f;
            Beam(line == 0 ? Trim : Frame, new Vector3(x, 1.25f, -7.38f),
                new Vector3(x, 1.25f, -5.14f), .11f);
        }
    }

    void HabitatRoom(Vector3 center, float halfX, float halfZ, float cut, float top, int windows, int doors)
    {
        Vector2[] footprint =
        {
            new Vector2(-halfX + cut, -halfZ), new Vector2(halfX - cut, -halfZ),
            new Vector2(halfX, -halfZ + cut), new Vector2(halfX, halfZ - cut),
            new Vector2(halfX - cut, halfZ), new Vector2(-halfX + cut, halfZ),
            new Vector2(-halfX, halfZ - cut), new Vector2(-halfX, -halfZ + cut)
        };
        Vector3 P(Vector2 point, float height) => center + new Vector3(point.x, height, point.y);
        void Band(int material, Vector2 left, Vector2 right, float bottom, float ceiling)
        {
            Quad(material, P(left, bottom), P(left, ceiling), P(right, ceiling), P(right, bottom));
            if (bottom >= .8f)
                Quad(Interior, P(right * .994f, bottom), P(right * .994f, ceiling),
                    P(left * .994f, ceiling), P(left * .994f, bottom));
        }

        for (int i = 0; i < footprint.Length; i++)
        {
            Vector2 a = footprint[i];
            Vector2 b = footprint[(i + 1) % footprint.Length];
            Band(Dark, a * .91f, b * .91f, -2.64f, .52f);
            Quad(Frame, P(a * .91f, .52f), P(a, .82f), P(b, .82f), P(b * .91f, .52f));
            Triangle(Interior, center + Vector3.up * .86f, P(b, .86f), P(a, .86f));
            Quad(Shell, P(a, top - .35f), P(a * .91f, top), P(b * .91f, top), P(b, top - .35f));
            Triangle(Shell, center + Vector3.up * top, P(b * .91f, top), P(a * .91f, top));
            Triangle(Interior, center + Vector3.up * (top - .17f),
                P(a * .94f, top - .17f), P(b * .94f, top - .17f));

            if ((doors & (1 << i)) != 0)
            {
                Vector2 midpoint = (a + b) * .5f;
                Vector2 edge = (b - a).normalized * 1.31f;
                Band(Shell, a, midpoint - edge, .86f, top - .34f);
                Band(Shell, midpoint + edge, b, .86f, top - .34f);
                Band(Frame, midpoint - edge, midpoint + edge, 3.58f, top - .34f);
                continue;
            }
            if ((windows & (1 << i)) == 0)
            {
                Band(Shell, a, b, .86f, top - .34f);
                continue;
            }

            const float sill = 1.55f;
            float header = top - .73f;
            Band(Shell, a, b, .86f, sill);
            Band(Shell, a, b, header, top - .34f);
            Pane(P(b, sill), P(a, sill), P(a, header), P(b, header));
            Beam(Frame, P(a, sill), P(b, sill), .24f);
            Beam(Frame, P(a, header), P(b, header), .24f);
            for (int post = 0; post <= 3; post++)
            {
                Vector2 point = Vector2.Lerp(a, b, post / 3f);
                Beam(Frame, P(point, sill), P(point, header), post == 0 || post == 3 ? .25f : .14f);
            }
        }
    }

    void PressureLink(float x)
    {
        Box(Frame, new Vector3(x, .71f, .4f), new Vector3(1.8f, .26f, 2.64f));
        Box(Frame, new Vector3(x, 3.72f, .4f), new Vector3(1.9f, .28f, 2.78f));
        for (int wall = -1; wall <= 1; wall += 2)
        {
            Box(Dark, new Vector3(x, 2.18f, .4f + wall * 1.31f), new Vector3(1.81f, 2.89f, .16f));
            Box(Trim, new Vector3(x, 1.33f, .4f + wall * 1.42f), new Vector3(1.5f, .15f, .10f));
        }
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
        if (glassMesh != null) Destroy(glassMesh);
        if (mesh != null) Destroy(mesh);
        if (materials == null) return;
        foreach (Material material in materials) Destroy(material);
    }
}
