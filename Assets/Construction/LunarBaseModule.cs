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
        bool solar = kind == BuildingKind.SolarStation;
        if (solar) BuildSolarStation();
        else if (habitat) BuildHabitat();
        else BuildCommand();

        mesh = new Mesh { name = solar ? "Solar arrays and power equipment" : habitat ? "Habitat pressure modules" : "Command pressure hull" };
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
            Surface(solar ? "Photovoltaic cells" : "Padded seats and bedding", solar ? new Color(.038f, .09f, .21f) : habitat ? new Color(.28f, .40f, .42f) : new Color(.14f, .27f, .34f), solar ? .29f : .02f, solar ? .52f : .29f)
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

        if (glassVertices.Count == 0) return;
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
        const float groundDeck = 1.47f;
        const float domeBase = 1.57f;
        const float radius = 10.6f;
        const int sides = 48;
        const int bands = 14;

        Axial(Dark, Vector3.zero, Vector3.up, 24, (-2.64f, 10.6f), (.12f, 11.37f), (.64f, 11.59f));
        Axial(Frame, Vector3.zero, Vector3.up, 24, (.64f, 11.59f), (.96f, 11.60f), (1.36f, 10.88f));
        Axial(Interior, Vector3.zero, Vector3.up, 48, (1.36f, 10.75f), (groundDeck, 10.75f));
        // An annular pressure collar, with no opaque disk above the living area.
        for (int segment = 0; segment < sides; segment++)
        {
            if (segment == 11 || segment == 12) continue; // Interrupted by the entrance collar.
            float a = segment * Mathf.PI * 2f / sides;
            float b = (segment + 1) * Mathf.PI * 2f / sides;
            Vector3 P(float angle, float r, float y) => new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
            Quad(Frame, P(a, 10.4f, 1.66f), P(b, 10.4f, 1.66f),
                P(b, 10.89f, 1.66f), P(a, 10.89f, 1.66f));
            Quad(Dark, P(a, 10.89f, 1.66f), P(b, 10.89f, 1.66f),
                P(b, 10.89f, 1.33f), P(a, 10.89f, 1.33f));
        }

        Vector3 Dome(int meridian, int band, float offset = 0f)
        {
            float longitude = meridian * Mathf.PI * 2f / sides;
            float altitude = band * Mathf.PI * .5f / bands;
            float size = radius + offset;
            return new Vector3(Mathf.Cos(longitude) * Mathf.Cos(altitude) * size,
                domeBase + Mathf.Sin(altitude) * size, Mathf.Sin(longitude) * Mathf.Cos(altitude) * size);
        }

        for (int band = 0; band < bands; band++)
        for (int segment = 0; segment < sides; segment++)
        {
            Vector3 a = Dome(segment, band);
            Vector3 b = Dome(segment + 1, band);
            Vector3 high = Dome(segment, band + 1);
            // The rectangular collar seals the glass opening at the entrance.
            bool entrance = band < 2 && (segment == 11 || segment == 12);
            // Wind panes outward so the far side cannot blend over the interior.
            bool shield = band == 0 || band >= 8;
            int skin = band == 0 ? Frame : (segment / 4 % 2 == 0 ? Shell : Frame);
            if (band == bands - 1)
                Triangle(skin, a, high, b);
            else if (!entrance)
            {
                Vector3 next = Dome(segment + 1, band + 1);
                if (shield) Quad(skin, a, high, next, b);
                else Pane(a, high, next, b);
            }

            if (segment % 4 == 0 && !entrance)
                Beam(Frame, Dome(segment, band, .057f), Dome(segment, band + 1, .057f), .15f);
            if (band == 3 || band == 7 || band == 11)
                Beam(Frame, Dome(segment, band, .067f), Dome(segment + 1, band, .067f), .10f);
            if (band == 0 && segment % 4 == 0)
            {
                float angle = segment * Mathf.PI * 2f / sides;
                Vector3 mount = new Vector3(Mathf.Cos(angle) * 10.87f, 1.51f, Mathf.Sin(angle) * 10.87f);
                Quaternion bearing = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                Box(Dark, mount, new Vector3(.72f, .25f, .67f), bearing);
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 side = Vector3.Cross(Vector3.up, outward);
                Beam(Frame, mount - outward * .27f + Vector3.up * .59f,
                    mount + outward * .54f - Vector3.up * .60f, .13f);
                for (int fastener = -1; fastener <= 1; fastener += 2)
                    Box(Frame, mount + side * (fastener * .23f) + Vector3.up * .14f,
                        new Vector3(.12f, .075f, .12f), bearing);
            }
        }
        Axial(Shell, Vector3.zero, Vector3.up, 16, (12.13f, .56f), (12.25f, .46f));

        // Four semi-open sleeping rooms surround a central common room.
        for (int side = -1; side <= 1; side += 2)
        for (int room = -1; room <= 1; room += 2)
        {
            float x = side * 5.69f;
            float z = room * 4.11f;
            Quaternion facing = Quaternion.Euler(0f, side * 90f, 0f);
            Bed(new Vector3(x, groundDeck, z + .19f), facing);
            Bed(new Vector3(x, groundDeck, z + 1.41f), facing);
            Box(Interior, new Vector3(x, 2.68f, z - 1.65f), new Vector3(3.05f, 2.4f, .16f));
            Box(Frame, new Vector3(x + side * 1.72f, 2.59f, z - .91f), new Vector3(.72f, 2.23f, 1.25f));
            for (int handle = -1; handle <= 1; handle++)
                Box(Trim, new Vector3(x + side * 1.34f, 2.45f + handle * .42f, z - .75f),
                    new Vector3(.05f, .16f, .08f));
            Box(Dark, new Vector3(x - side * 1.68f, 2.27f, z - .90f), new Vector3(.60f, 1.56f, 1.39f));
            Box(Interior, new Vector3(x - side * 1.68f, 3.08f, z - .90f), new Vector3(.88f, .11f, 1.54f));
            Box(Window, new Vector3(x, 3.74f, z - 1.49f), new Vector3(2.07f, .077f, .13f));
        }

        // Dining tables, an equipment bench and a lounge group stay in the open.
        Axial(Interior, new Vector3(-.7f, 0f, 1.66f), Vector3.up, 12, (1.47f, .42f), (2.22f, .64f));
        Axial(Shell, new Vector3(-.7f, 0f, 1.66f), Vector3.up, 16, (2.22f, 1.4f), (2.38f, 1.4f));
        for (int seat = 0; seat < 4; seat++)
        {
            Quaternion turn = Quaternion.Euler(0f, seat * 90f, 0f);
            Vector3 position = new Vector3(-.7f, groundDeck, 1.66f) + turn * (Vector3.back * 2.03f);
            Chair(position, turn);
        }
        Box(Seat, new Vector3(-.1f, 1.96f, -5.97f), new Vector3(3.97f, .75f, 1.02f));
        Box(Seat, new Vector3(-.1f, 2.58f, -6.34f), new Vector3(4.02f, .84f, .31f));
        Box(Interior, new Vector3(-.1f, 1.88f, -4.69f), new Vector3(2.38f, .28f, .77f));
        Box(Frame, new Vector3(2.8f, 2.24f, -4.69f), new Vector3(1.39f, 1.52f, .87f));
        Box(Window, new Vector3(2.80f, 3.05f, -4.54f), new Vector3(.84f, .22f, .52f));
        Box(Beacon, new Vector3(-.1f, 1.486f, 6.48f), new Vector3(1.2f, .034f, 3.66f));
        InteriorLight(new Vector3(0f, 5.45f, 2.05f), new Color(1f, .73f, .44f), 11.8f, 27f);
        InteriorLight(new Vector3(0f, 5.02f, -4.1f), new Color(1f, .72f, .42f), 10.9f, 20f);

        Entry(9.86f, 12.71f, 2.1f);
        Box(Frame, new Vector3(0f, 3.84f, 10.46f), new Vector3(4.26f, .44f, 1.34f));
        // Short threshold ties the flush collar and the raised inner floor to the airlock.
        Quad(Frame, new Vector3(-1.72f, 1.48f, 9.58f), new Vector3(-1.72f, .83f, 11.21f),
            new Vector3(1.72f, .83f, 11.21f), new Vector3(1.72f, 1.48f, 9.58f));

        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 cabinet = new Vector3(side * 10.29f, .83f, -2.76f);
            Equipment(cabinet, 1.35f, 2.10f);
            Vector3 mast = new Vector3(side * 9.66f, 1.36f, -6.14f);
            Box(Dark, mast, new Vector3(.73f, .24f, .69f));
            Beam(Frame, mast, mast + Vector3.up * 3.16f, .10f);
            Vector3 bar = mast + Vector3.up * 2.25f;
            Beam(Frame, bar + Vector3.left * .56f, bar + Vector3.right * .56f, .083f);
            Box(Beacon, mast + Vector3.up * 3.19f, new Vector3(.19f, .21f, .19f));
        }
        Box(Frame, new Vector3(0f, 1.2f, -11.27f), new Vector3(2.15f, .88f, .32f));
        Box(Dark, new Vector3(0f, 1.2f, -11.45f), new Vector3(1.61f, .69f, .085f));
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

    void Bed(Vector3 position, Quaternion turn)
    {
        Vector3 P(float x, float y, float z) => position + turn * new Vector3(x, y, z);
        Box(Frame, P(0f, .25f, 0f), new Vector3(1.04f, .5f, 2.13f), turn);
        Box(Seat, P(0f, .57f, 0f), new Vector3(.96f, .18f, 1.96f), turn);
        Box(Shell, P(0f, .71f, -.70f), new Vector3(.76f, .14f, .44f), turn);
        Box(Interior, P(0f, .67f, .26f), new Vector3(.94f, .07f, 1.1f), turn);
        Box(Dark, P(0f, .38f, 1.42f), new Vector3(.72f, .76f, .63f), turn);
        Box(Trim, P(0f, .49f, 1.75f), new Vector3(.23f, .07f, .055f), turn);
    }

    void BuildSolarStation()
    {
        Axial(Dark, Vector3.zero, Vector3.up, 12, (-2.64f, 12.45f), (.15f, 12.62f));
        Axial(Frame, Vector3.zero, Vector3.up, 12, (.15f, 12.62f), (.52f, 12.62f), (.65f, 12.22f));
        Quaternion tilt = Quaternion.Euler(21f, 0f, 0f);
        for (int row = -1; row <= 1; row++)
        for (int column = -1; column <= 1; column += 2)
        {
            Vector3 pivot = new Vector3(column * 4.18f, 3.22f, row * 5.65f - .62f);
            Vector3 P(float x, float y, float z) => pivot + tilt * new Vector3(x, y, z);
            Box(Dark, P(0f, -.14f, 0f), new Vector3(6.75f, .23f, 4.60f), tilt);
            for (int i = -1; i <= 1; i += 2)
            {
                Box(Frame, P(i * 3.35f, .025f, 0f), new Vector3(.12f, .16f, 4.61f), tilt);
                Box(Frame, P(0f, .025f, i * 2.24f), new Vector3(6.75f, .16f, .13f), tilt);
                Box(Frame, new Vector3(pivot.x + i * 2.16f, .75f, pivot.z), new Vector3(.62f, .20f, .86f));
                Beam(Frame, new Vector3(pivot.x + i * 2.16f, .82f, pivot.z), P(i * 2.16f, -.21f, 0f), .22f);
                Beam(Dark, new Vector3(pivot.x + i * 2.16f, 1.60f, pivot.z), P(i * 2.16f, -.24f, 1.34f), .135f);
            }
            for (int x = 0; x < 8; x++)
            for (int z = 0; z < 6; z++)
                Box(Seat, P((x - 3.5f) * .815f, -.003f, (z - 2.5f) * .716f),
                    new Vector3(.771f, .041f, .676f), tilt);
            Beam(Frame, P(-3.12f, -.24f, 0f), P(3.12f, -.24f, 0f), .19f);
            Beam(Dark, new Vector3(pivot.x, .69f, pivot.z), new Vector3(column * .60f, .69f, pivot.z), .1f);
        }
        for (int side = -1; side <= 1; side += 2)
            Beam(Dark, new Vector3(side * .60f, .69f, -6.6f), new Vector3(side * .60f, .69f, 9.7f), .13f);
        Equipment(new Vector3(-1.55f, .64f, 9.07f), 2.18f, 2.05f);
        Equipment(new Vector3(1.56f, .64f, 9.07f), 2.34f, 2.05f);
        for (int lamp = -1; lamp <= 1; lamp++)
            Box(Beacon, new Vector3(lamp * .36f + 1.55f, 1.60f, 10.12f), new Vector3(.15f, .10f, .08f));
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
