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
    static readonly Dictionary<(BuildingKind kind, Material template), ModuleAssets> common =
        new Dictionary<(BuildingKind,Material),ModuleAssets>();
    ModuleAssets assets;
    sealed class ModuleAssets
    {
        internal Mesh ShellMesh, GlassMesh;
        internal Material[] Palette;
        internal int Users;
        internal readonly List<(Vector3 position,Color color,float range,float intensity)> Lamps =
            new List<(Vector3,Color,float,float)>();
    }
    readonly List<Light> interiorLights = new List<Light>();
    Color windowColor, beaconColor, windowEmission, beaconEmission, liningEmission;
    bool powered = true, processing;
    Transform processRotor;
    float operatingTime;
    Transform waterBit;
    readonly Transform[] coolingRotors = new Transform[2];

    void Awake()
    {
        bool habitat = kind == BuildingKind.Habitat;
        bool solar = kind == BuildingKind.SolarStation;
        bool greenhouse = kind == BuildingKind.Greenhouse;
        bool mining = kind == BuildingKind.MaterialsStation;
        bool utility = kind == BuildingKind.WaterStation || kind == BuildingKind.OxygenStation;
        var key=(kind,urpLitTemplate);
        if(!common.TryGetValue(key,out assets))
        {
            assets=new ModuleAssets();
            common.Add(key,assets);
        }
        assets.Users++;
        if(assets.ShellMesh==null)
        {
            switch (kind)
            {
                case BuildingKind.ScienceLab: BuildScienceLab(); break;
                case BuildingKind.Refinery: BuildProcessingHall(false); break;
                case BuildingKind.FuelWorks: BuildProcessingHall(true); break;
                case BuildingKind.SolarStation: BuildSolarStation(); break;
                case BuildingKind.WaterStation: BuildWaterStation(); break;
                case BuildingKind.OxygenStation: BuildOxygenStation(); break;
                case BuildingKind.MaterialsStation: BuildMaterialsStation(); break;
                case BuildingKind.AirlockCapsule: BuildAirlock(); break;
                case BuildingKind.CargoHangar: BuildCargoHangar(); break;
                case BuildingKind.DockingNode: BuildDockingNode(); break;
                case BuildingKind.Greenhouse: BuildGreenhouse(); break;
                case BuildingKind.Habitat: BuildHabitat(); break;
                default: BuildCommand(); break;
            }
            mesh = new Mesh { name = kind.ToString() + " geometry" };
            if (vertices.Count > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.subMeshCount = faces.Length;
            for (int i = 0; i < faces.Length; i++) mesh.SetTriangles(faces[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            assets.ShellMesh=mesh;
        }
        else foreach(var fixture in assets.Lamps)
            CreateInteriorLight(fixture.position,fixture.color,fixture.range,fixture.intensity);
        mesh=assets.ShellMesh;
        vertices.Clear();
        foreach (List<int> face in faces) face.Clear();

        assets.Palette ??= new[]
        {
            Surface("Ceramic-coated pressure panels", new Color(.77f, .80f, .78f), .34f, .33f),
            Surface("Brushed aluminium frames", new Color(.33f, .42f, .47f), .63f, .46f),
            Surface("Graphite thermal shielding", new Color(.095f, .125f, .147f), .36f, .35f),
            Surface("Orange safety markings", new Color(.87f, .41f, .11f), .23f, .35f),
            Surface("Lit pressure windows", habitat ? new Color(.91f, .59f, .28f) : new Color(.21f, .61f, .81f), .16f, .55f),
            Surface("Approach beacons", mining ? new Color(.8f, .34f, .075f) : utility ? new Color(.11f, .47f, .8f) : new Color(.14f, .8f, .65f), .05f, .48f),
            Surface("Interior wall and floor lining", habitat ? new Color(.68f, .55f, .45f) : greenhouse ? new Color(.31f, .54f, .16f) : new Color(.49f, .57f, .63f), .09f, .48f),
            Surface(solar ? "Photovoltaic cells" : greenhouse ? "Crop leaves" : "Padded seats and bedding", solar ? new Color(.038f, .09f, .21f) : greenhouse ? new Color(.18f, .43f, .12f) : habitat ? new Color(.28f, .40f, .42f) : new Color(.14f, .27f, .34f), solar ? .29f : .02f, solar ? .52f : .29f)
        };
        materials=(Material[])assets.Palette.Clone();
        materials[Window]=new Material(materials[Window]);
        materials[Beacon]=new Material(materials[Beacon]);
        materials[Interior]=new Material(materials[Interior]);
        materials[Window].EnableKeyword("_EMISSION");
        materials[Window].SetColor("_EmissionColor",
            habitat ? new Color(.83f, .37f, .09f) * 1.14f : new Color(.09f, .49f, .77f) * 1.28f);
        materials[Beacon].EnableKeyword("_EMISSION");
        materials[Beacon].SetColor("_EmissionColor", mining ? new Color(.63f, .20f, .027f) : utility ? new Color(.07f, .32f, .63f) : new Color(.05f, .83f, .69f) * 1.6f);
        GetComponent<MeshFilter>().sharedMesh = mesh;
        GetComponent<MeshRenderer>().sharedMaterials = materials;
        if (kind == BuildingKind.DockingNode || kind == BuildingKind.CargoHangar || kind == BuildingKind.AirlockCapsule || kind == BuildingKind.ScienceLab ||
            kind == BuildingKind.Refinery || kind == BuildingKind.FuelWorks)
        {
            GetComponent<BoxCollider>().enabled = false;
            gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
        materials[Interior].EnableKeyword("_EMISSION");
        materials[Interior].SetColor("_EmissionColor",
            habitat ? new Color(.15f, .095f, .055f) : new Color(.077f, .125f, .16f));

        windowColor = materials[Window].GetColor("_BaseColor");
        beaconColor = materials[Beacon].GetColor("_BaseColor");
        windowEmission = materials[Window].GetColor("_EmissionColor");
        beaconEmission = materials[Beacon].GetColor("_EmissionColor");
        liningEmission = materials[Interior].GetColor("_EmissionColor");
        if (mining) BuildMovingMine();
        if (kind == BuildingKind.OxygenStation) BuildCoolingRotors();
        if (kind == BuildingKind.AirlockCapsule) BuildSurfaceDoor();
        if (kind == BuildingKind.CargoHangar)
        {
            BuildCargoDoor();
            CargoHangarRamp.Add(transform, materials);
        }
        if (kind == BuildingKind.WaterStation)
        {
            waterBit = Machinery("Water ice drill shaft", new Vector3(5.5f, 0f, 1.6f), () =>
                Axial(Frame, Vector3.zero, Vector3.up, 16, (-.38f, .31f), (1.15f, .47f), (8.26f, .301f)));
        }
        if(kind==BuildingKind.Refinery || kind==BuildingKind.FuelWorks)
        {
            processRotor=Machinery("Industrial process pump",new Vector3(7.65f,2.02f,7.78f),()=>
            {
                Axial(Frame,Vector3.zero,Vector3.forward,12,(-.15f,.48f),(.13f,.48f));
                for(int blade=0;blade<6;blade++)
                {
                    Quaternion angle=Quaternion.Euler(0,0,blade*60f);
                    Box(Trim,angle*new Vector3(0,.35f,.15f),new Vector3(.12f,.42f,.14f),angle);
                }
            });
        }
        enabled=CanAnimate();
        if (glassVertices.Count == 0 && assets.GlassMesh==null)
        {
            ReleaseBuildBuffers();
            return;
        }
        if(assets.GlassMesh==null)
        {
            glassMesh = new Mesh { name = "Cabin and module glazing" };
            glassMesh.SetVertices(glassVertices);
            var triangles = new int[glassVertices.Count];
            for (int i = 0; i < triangles.Length; i++) triangles[i] = i;
            glassMesh.SetTriangles(triangles, 0);
            glassMesh.RecalculateNormals();
            glassMesh.RecalculateBounds();
            assets.GlassMesh=glassMesh;
        }
        glassMesh=assets.GlassMesh;
        glassVertices.Clear();
        var windows = new GameObject("Transparent pressure glazing", typeof(MeshFilter), typeof(MeshRenderer));
        windows.transform.SetParent(transform, false);
        windows.GetComponent<MeshFilter>().sharedMesh = glassMesh;
        var renderer = windows.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = glazing;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (kind == BuildingKind.DockingNode || kind == BuildingKind.CargoHangar || kind == BuildingKind.AirlockCapsule || kind == BuildingKind.ScienceLab ||
            kind == BuildingKind.Refinery || kind == BuildingKind.FuelWorks) windows.AddComponent<MeshCollider>().sharedMesh = glassMesh;
        ReleaseBuildBuffers();
    }

    internal void SetPowered(bool enabled)
    {
        powered = enabled;
        this.enabled = CanAnimate();
        foreach (Light light in interiorLights) light.enabled = enabled;
        materials[Window].SetColor("_BaseColor", enabled ? windowColor : windowColor * .23f);
        materials[Beacon].SetColor("_BaseColor", enabled ? beaconColor : beaconColor * .28f);
        materials[Window].SetColor("_EmissionColor", enabled ? windowEmission : Color.black);
        materials[Beacon].SetColor("_EmissionColor", enabled ? beaconEmission : Color.black);
        materials[Interior].SetColor("_EmissionColor", enabled ? liningEmission : Color.black);
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

        Entry(9.5f, 13.84f, 2.16f, true);
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
        for (int step = 0; step < 3; step++)
        {
            float top = .62f - step * .18f;
            Box(Frame, new Vector3(0f, (top - 2.60f) * .5f, 13.04f + step * .39f),
                new Vector3(3.56f, top + 2.60f, .43f));
        }
        for (int side = -1; side <= 1; side += 2)
        {
            Box(Trim, new Vector3(side * 2.24f, 1.70f, 12.86f), new Vector3(.08f, .46f, .16f));
            Box(Beacon, new Vector3(side * 1.89f, 3.42f, 12.87f), new Vector3(.16f, .13f, .13f));
            Equipment(new Vector3(side * 7.7f, .70f, 7.45f), 1.04f, 1.27f);
        }
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

    void Entry(float start, float end, float halfWidth, bool decorativeDoor = false)
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



        if(decorativeDoor)
        {
            Box(Dark, new Vector3(0f, 2.09f, end - .02f), new Vector3(3.27f, 2.69f, .22f));
            Box(Frame, new Vector3(0f, 2.08f, end + .105f), new Vector3(2.34f, 2.34f, .10f));
            Box(Dark, new Vector3(0f, 2.08f, end + .161f), new Vector3(.043f, 2.12f, .03f));
        }
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

    public Transform SurfaceDoor { get; private set; }
    public bool SurfaceDoorOpen => SurfaceDoor != null && !SurfaceDoor.gameObject.activeSelf;

    public void ToggleSurfaceDoor()
    {
        if (SurfaceDoor != null) SurfaceDoor.gameObject.SetActive(SurfaceDoorOpen);
    }

    void BuildAirlock()
    {
        const float floor = .62f;
        Foundation(0f, 0f, 9f, 10f);
        Box(Interior, new Vector3(0f,.575f,0f),new Vector3(8.90f,.09f,9.90f));
        Box(Shell,new Vector3(0f,4.15f,0f),new Vector3(8.60f,.49f,9.71f));
        Box(Frame,new Vector3(0f,4.46f,0f),new Vector3(7.83f,.18f,8.89f));
        for (int side=-1;side<=1;side+=2)
        {
            Box(Frame,new Vector3(side*4.36f,4.0f,0f),new Vector3(.21f,.34f,9.58f));
            Box(Frame,new Vector3(side*4.4f,3.45f,side*4.85f),new Vector3(.31f,1.06f,.29f));
            Box(Window,new Vector3(side*.91f,3.45f,5.02f),new Vector3(.29f,.12f,.072f));
            Equipment(new Vector3(side*2.58f,floor,-3.26f),1.12f,1.46f);
            for (int tank=0;tank<2;tank++)
            {
                Vector3 center=new Vector3(side*(2.44f+tank*.57f),0f,3.63f);
                Axial(Frame,center,Vector3.up,14,(.64f,.24f),(.77f,.27f),(2.19f,.27f),(2.39f,.13f));
                Axial(Trim,center,Vector3.up,14,(1.25f,.281f),(1.34f,.281f));
                Box(Window,center+new Vector3(0f,2.05f,-.278f),new Vector3(.19f,.14f,.045f));
            }
            for (int suit=0;suit<2;suit++)
            {
                Vector3 root=new Vector3(side*(2.27f+suit*.75f),floor,-1.42f);
                Box(Dark,root+new Vector3(0f,1.08f,.31f),new Vector3(.61f,2.06f,.17f));
                Box(Shell,root+Vector3.up*1.19f,new Vector3(.37f,.59f,.231f));
                Box(Frame,root+new Vector3(0f,1.23f,.17f),new Vector3(.33f,.52f,.31f));
                Axial(Shell,root,Vector3.up,12,(1.51f,.11f),(1.61f,.18f),(1.79f,.18f),(1.86f,.105f));
                Box(Dark,root+new Vector3(0f,1.72f,-.16f),new Vector3(.24f,.13f,.07f));
                foreach (float bodySide in new[]{-1f,1f})
                {
                    Beam(Shell,root+new Vector3(bodySide*.19f,1.42f,0f),
                        root+new Vector3(bodySide*.27f,.90f,-.055f),.102f);
                    Beam(Shell,root+new Vector3(bodySide*.095f,.91f,0f),
                        root+new Vector3(bodySide*.104f,.32f,0f),.155f);
                }
            }
        }

        Wall(0f,-5f,180f,9f);
        Wall(4.5f,0f,90f,10f);
        Wall(-4.5f,0f,-90f,10f);
        Wall(0f,5f,0f,9f);
        Box(Frame,new Vector3(0f,.53f,5.28f),new Vector3(3.22f,.17f,.80f));
        for (int step=0;step<4;step++)
        {
            float top=.47f-step*.115f;
            Box(Frame,new Vector3(0f,(top-1.1f)*.5f,5.75f+step*.40f),new Vector3(3.22f,top+1.1f,.42f));
        }
        foreach (float side in new[]{-1f,1f})
        {
            Beam(Frame,new Vector3(side*1.57f,1.46f,5.16f),new Vector3(side*1.57f,.96f,7.18f),.083f);
            Beam(Frame,new Vector3(side*1.57f,.14f,7.18f),new Vector3(side*1.57f,.96f,7.18f),.095f);
        }
        Box(Window,new Vector3(0f,3.86f,0f),new Vector3(2.11f,.065f,3.11f));
        InteriorLight(new Vector3(0f,3.42f,0f),new Color(1f,.8f,.57f),4.85f,2.0f);

        void Wall(float x,float z,float yaw,float span)
        {
            Quaternion turn=Quaternion.Euler(0f,yaw,0f);
            Vector3 P(float width,float height,float depth=0f)=>new Vector3(x,height,z)+turn*new Vector3(width,0f,depth);
            float wing=span*.5f-1.4f;
            for(int side=-1;side<=1;side+=2)
            {
                float mid=side*(1.4f+wing*.5f);
                Box(Shell,P(mid,1.37f,-.09f),new Vector3(wing,1.50f,.19f),turn);
                Box(Shell,P(mid,3.57f,-.09f),new Vector3(wing,1.17f,.19f),turn);
                Vector3 a=P(side*1.55f,2.12f), b=P(side*(span*.5f-.24f),2.12f);
                if (side>0) Pane(a,b,b+Vector3.up*.87f,a+Vector3.up*.87f);
                else Pane(b,a,a+Vector3.up*.87f,b+Vector3.up*.87f);
                Box(Frame,P(side*1.47f,1.90f),new Vector3(.18f,2.64f,.22f),turn);
                Box(Frame,P(side*(span*.5f-.13f),2.51f),new Vector3(.23f,.91f,.19f),turn);
            }
            Box(Frame,P(0f,3.59f),new Vector3(2.98f,1.06f,.22f),turn);
            Box(Frame,P(0f,.575f),new Vector3(2.82f,.09f,.17f),turn);
        }
    }

    void BuildSurfaceDoor()
    {
        SurfaceDoor=Machinery("Surface pressure door",new Vector3(0f,0f,4.96f),()=>{
            Box(Frame,new Vector3(0f,1.935f,0f),new Vector3(2.80f,2.63f,.24f));
            Box(Dark,new Vector3(0f,1.93f,.129f),new Vector3(2.39f,2.29f,.062f));
            Box(Window,new Vector3(0f,2.47f,.175f),new Vector3(.60f,.30f,.05f));
            Box(Trim,new Vector3(.82f,1.48f,.19f),new Vector3(.15f,.33f,.071f));
        });
        var collision=SurfaceDoor.gameObject.AddComponent<BoxCollider>();
        collision.center=new Vector3(0f,1.935f,0f);
        collision.size=new Vector3(2.80f,2.63f,.24f);
    }

    public Transform CargoGate { get; private set; }

    void BuildCargoHangar()
    {
        int frameStart=vertices.Count;
        int paneStart=glassVertices.Count;
        Foundation(0f,0f,32f,22f);
        const float floor=.62f;
        Box(Interior,new Vector3(0f,.571f,0f),new Vector3(31.6f,.10f,21.8f));
        // Chamfered roof and corner modules leave the operations hall unobstructed.
        for(int side=-1;side<=1;side+=2)
        {
            float x=side*15.82f;
            Box(Shell,new Vector3(x,1.47f,0f),new Vector3(.35f,1.70f,19.0f));
            Box(Shell,new Vector3(x,6.30f,0f),new Vector3(.35f,2.70f,19.0f));
            for(int bay=-3;bay<=3;bay++)
            {
                float z=bay*2.65f;
                Box(Frame,new Vector3(x,3.61f,z),new Vector3(.50f,2.90f,.17f));
                Vector3 a=new Vector3(x,2.32f,z-1.25f),b=new Vector3(x,2.32f,z+1.25f);
                if(side>0) Pane(b,a,a+Vector3.up*2.53f,b+Vector3.up*2.53f);
                else Pane(a,b,b+Vector3.up*2.53f,a+Vector3.up*2.53f);
                Box(Frame,new Vector3(x,4.88f,z),new Vector3(.53f,.13f,2.62f));
            }
            for(int end=-1;end<=1;end+=2)
            {
                Box(Shell,new Vector3(side*15.15f,4.02f,end*10.1f),
                    new Vector3(2.15f,6.83f,.52f),Quaternion.Euler(0f,-side*end*43f,0f));
                Box(Frame,new Vector3(side*15.19f,7.44f,end*10.14f),
                    new Vector3(2.29f,.31f,.60f),Quaternion.Euler(0f,-side*end*43f,0f));
            }
            Box(Shell,new Vector3(side*10.22f,1.42f,10.91f),new Vector3(9.14f,1.62f,.27f));
            Box(Shell,new Vector3(side*10.22f,6.16f,10.91f),new Vector3(9.14f,3.02f,.27f));
            for (int column = 0; column <= 4; column++)
            {
                float facadeX = side * (5.65f + column * 2.285f);
                Box(Frame, new Vector3(facadeX, 3.44f, 10.94f), new Vector3(.19f, 2.48f, .27f));
                if (column == 4) continue;
                Vector3 a = new Vector3(facadeX, 2.22f, 11.04f);
                Vector3 b = a + Vector3.right * (side * 2.285f);
                if (side > 0) Pane(a,b,b+Vector3.up*2.44f,a+Vector3.up*2.44f);
                else Pane(b,a,a+Vector3.up*2.44f,b+Vector3.up*2.44f);
            }
            Box(Frame,new Vector3(side*5.66f,3.83f,11.0f),new Vector3(.36f,6.51f,.46f));
            Box(Window,new Vector3(side*6.01f,6.62f,11.08f),new Vector3(.62f,.23f,.11f));
            Box(Shell,new Vector3(side*8.06f,4.13f,-10.91f),new Vector3(13.30f,7.08f,.27f));
            Box(Frame,new Vector3(side*1.51f,2.03f,-11.0f),new Vector3(.18f,2.81f,.28f));
            Beam(Trim,new Vector3(side*5.3f,floor+.018f,-8.8f),new Vector3(side*5.3f,floor+.018f,10.4f),.045f);
            Equipment(new Vector3(side*7.58f,floor,1.69f),2.09f,2.69f);
            Box(Window,new Vector3(side*7.58f,1.70f,3.10f),new Vector3(.53f,.24f,.055f));
        }
        Box(Frame,new Vector3(0f,7.02f,11.0f),new Vector3(11.35f,.35f,.52f));
        Box(Shell,new Vector3(0f,7.43f,10.94f),new Vector3(11.38f,.82f,.29f));
        Box(Shell,new Vector3(0f,4.12f,-10.99f),new Vector3(3.12f,7.14f,.31f));

        for (int i=frameStart;i<vertices.Count;i++) vertices[i]=Raise(vertices[i]);
        for (int i=paneStart;i<glassVertices.Count;i++) glassVertices[i]=Raise(glassVertices[i]);
        static Vector3 Raise(Vector3 point)
        {
            point.y=point.y<=.62f?point.y:.62f+(point.y-.62f)*1.37f;
            return point;
        }
        const int arcs = 26;
        Vector3 Roof(int index, float z, float shrink = 0f)
        {
            float t = index * Mathf.PI / arcs;
            return new Vector3(Mathf.Cos(t) * (15.91f - shrink), 10.25f + Mathf.Sin(t) * (5.74f - shrink), z);
        }
        for (int shell = 0; shell < arcs; shell++)
        {
            Vector3 a = Roof(shell,-11.12f), b = Roof(shell+1,-11.12f);
            Vector3 c = Roof(shell+1,11.12f), d = Roof(shell,11.12f);
            Quad(shell%6==0 ? Frame : Shell,a,b,c,d);
            Quad(Dark,d,c,b,a);
            Triangle(Frame,new Vector3(0f,10.25f,11.12f),d,c);
            Triangle(Frame,new Vector3(0f,10.25f,-11.12f),b,a);
            if (shell % 4 == 0) Beam(Frame,a+Vector3.up*.06f,d+Vector3.up*.06f,.12f);
        }
        for (int truss=-4;truss<=4;truss++)
        {
            float z=truss*2.73f;
            for(int arch=0;arch<arcs;arch++)
            {
                Vector3 a=Roof(arch,z,.18f), b=Roof(arch+1,z,.18f);
                Beam(Frame,a,b,.22f);
                if(arch%3==0)
                    Beam(Frame,a,Roof(arch+1,z-.34f,.56f),.105f);
            }
            for(int lane=-1;lane<=1;lane++)
            {
                Vector3 light=new Vector3(lane*5.45f,15.17f-Mathf.Abs(lane)*.40f,z);
                Box(Frame,light,new Vector3(.76f,.18f,1.27f));
                Box(Window,light-Vector3.up*.102f,new Vector3(.63f,.032f,1.12f));
            }
        }
        foreach (float side in new[] {-1f,1f})
            Beam(Frame,new Vector3(side*15.82f,10.31f,-11.06f),new Vector3(side*15.82f,10.31f,11.06f),.29f);
        for(int side=-1;side<=1;side+=2)
        {
            float x=side*3.03f;
            Box(Frame,new Vector3(x,.64f,6.5f),new Vector3(3.4f,.035f,4.3f));
            InteriorLight(new Vector3(x,13.2f,4.25f),new Color(1f,.83f,.66f),13.6f,6.2f);
            InteriorLight(new Vector3(x,13.2f,-4.5f),new Color(1f,.83f,.66f),13.6f,6.2f);
            for(int bin=0;bin<4;bin++)
            {
                float z=-6.7f+bin*2.62f;
                Box(Dark,new Vector3(side*12.58f,1.28f,z),new Vector3(2.2f,1.31f,1.73f));
                Box(Trim,new Vector3(side*12.58f,1.95f,z),new Vector3(2.25f,.12f,1.78f));
                for(int rib=-2;rib<=2;rib++)
                    Box(Frame,new Vector3(side*12.58f+rib*.38f,1.28f,z+.88f),new Vector3(.08f,1.24f,.08f));
            }
            for(int tier=0;tier<3;tier++)
            {
                Box(Frame,new Vector3(side*10.3f,1.05f+tier*.79f,-8.67f),new Vector3(4.1f,.12f,1.17f));
                for(int tool=0;tool<7;tool++)
                    Box(tool%3==0?Trim:Dark,new Vector3(side*10.3f-1.70f+tool*.53f,1.30f+tier*.79f,-8.64f),
                        new Vector3(.40f,.32f,.63f));
            }
        }
        Equipment(new Vector3(8.7f,14.95f,-5.5f),4.8f,2.2f);
        Beam(Frame,new Vector3(-8.2f,15.0f,-6.4f),new Vector3(-8.2f,17.57f,-6.4f),.12f);
        Beam(Frame,new Vector3(-9.45f,17.12f,-6.4f),new Vector3(-6.98f,17.12f,-6.4f),.10f);
        Box(Beacon,new Vector3(-8.2f,17.58f,-6.4f),new Vector3(.15f,.17f,.15f));
        Box(Trim,new Vector3(8.75f,floor+.034f,-1.35f),new Vector3(3.8f,.031f,3.5f));

    }

    void BuildCargoDoor()
    {
        // The separate gate mesh starts raised. A later controller can lower it on the guide rails.
        CargoGate=Machinery("Cargo gate (open)",new Vector3(0f,9.58f,11.03f),()=>{
            for(int blade=0;blade<6;blade++)
                Box(Frame,new Vector3(0f,blade*.16f,0f),new Vector3(10.83f,.135f,.26f));
            Box(Trim,new Vector3(0f,.02f,.16f),new Vector3(10.64f,.045f,.061f));
        });
    }

    void BuildDockingNode()
    {
        const int sides = 8;
        const float bottom = .57f, spring = 3.27f;
        const float radius = 4.82f;
        const float apothem = 4.4533f;
        Axial(Dark, Vector3.zero, Vector3.up, 8, (-2.45f, radius), (.43f, radius));
        Axial(Frame, Vector3.zero, Vector3.up, 8, (.43f, radius), (bottom, radius));
        Vector3 Corner(int index, float y, float scale)
        {
            float angle = (index + .5f) * Mathf.PI * 2f / sides;
            return new Vector3(radius * Mathf.Cos(angle) * scale, y, radius * Mathf.Sin(angle) * scale);
        }

        for (int face = 0; face < sides; face++)
        {
            Vector3 a = Corner(face, spring, 1f), b = Corner(face + 1, spring, 1f);
            Vector3 c = Corner(face + 1, 4.22f, .50f), d = Corner(face, 4.22f, .50f);
            Pane(b, a, d, c);
            Vector3 crown = new Vector3(0f, 4.22f, 0f);
            glassVertices.Add(crown);
            glassVertices.Add(c);
            glassVertices.Add(d);
            Beam(Frame, a, b, .105f);
            Beam(Frame, a, d, .11f);
            Beam(Frame, d, c, .085f);
            Beam(Frame, Corner(face, bottom, 1f), a, .14f);
            if (face % 2 == 0)
            {
                Vector3 floorA = Corner(face, bottom + .08f, .95f);
                Vector3 floorB = Corner(face + 1, bottom + .08f, .95f);
                Beam(Frame, floorA, floorB, .19f);
                Vector3 waistA = Corner(face, bottom + .42f, 1f);
                Vector3 waistB = Corner(face + 1, bottom + .42f, 1f);
                Quad(Shell, Corner(face + 1, bottom, 1f), Corner(face, bottom, 1f), waistA, waistB);
                Quad(Frame, b - Vector3.up * .22f, a - Vector3.up * .22f, a, b);
                Pane(waistB, waistA, a - Vector3.up * .22f, b - Vector3.up * .22f);
            }
        }

        for (int port = 0; port < 4; port++)
        {
            Quaternion turn = Quaternion.Euler(0f, port * 90f, 0f);
            Vector3 P(float x, float y, float z = apothem) => turn * new Vector3(x, y, z);
            foreach (float side in new[] { -1f, 1f })
            {
                Box(Shell, P(side * 1.62f, 1.86f, apothem - .085f), new Vector3(.44f, 2.59f, .17f), turn);
                Box(Frame, P(side * 1.49f, 1.84f), new Vector3(.13f, 2.60f, .17f), turn);
                Box(Beacon, P(side * 1.49f, .82f, apothem + .086f), new Vector3(.086f, .095f, .031f), turn);
                Beam(Window, P(side * 1.16f, bottom + .017f, 1.8f), P(side * 1.16f, bottom + .017f, 4.39f), .035f);
            }
            Box(Frame, P(0f, 3.235f), new Vector3(3.70f, .14f, .19f), turn);
            Box(Frame, P(0f, .542f), new Vector3(2.85f, .055f, .26f), turn);
        }
        InteriorLight(Vector3.up * 2.66f, new Color(1f, .79f, .57f), 2.45f, 1.20f);
    }

    void BuildScienceLab()
    {
        Foundation(0f,0f,18.9f,14.6f);
        const float deck=.62f;
        Box(Interior,new Vector3(0f,.60f,0f),new Vector3(18.4f,.10f,14f));
        Box(Shell,new Vector3(0f,4.77f,0f),new Vector3(17.4f,.48f,13.8f));
        Box(Frame,new Vector3(0f,5.04f,0f),new Vector3(14.8f,.13f,12.7f));
        for(int side=-1;side<=1;side+=2)
        {
            float x=side*9.18f;
            Box(Shell,new Vector3(x,.99f,0f),new Vector3(.29f,.75f,13.8f));
            Box(Frame,new Vector3(x,4.08f,0f),new Vector3(.48f,.61f,13.8f));
            for(int bay=-2;bay<=2;bay++)
            {
                float z=bay*2.66f;
                Vector3 a=new Vector3(x,1.34f,z-1.28f),b=new Vector3(x,1.34f,z+1.28f);
                if(side>0) Pane(b,a,a+Vector3.up*2.43f,b+Vector3.up*2.43f);
                else Pane(a,b,b+Vector3.up*2.43f,a+Vector3.up*2.43f);
                Box(Frame,new Vector3(x,2.54f,z-1.29f),new Vector3(.30f,2.6f,.17f));
            }
            Box(Shell,new Vector3(side*5.42f,2.4f,-7.13f),new Vector3(7.96f,3.66f,.37f));
            Box(Frame,new Vector3(side*1.5f,1.91f,-7.28f),new Vector3(.19f,2.67f,.29f));
            Equipment(new Vector3(side*6.65f,4.98f,-3.75f),2.10f,2.42f);
            for(int bench=0;bench<3;bench++)
            {
                Vector3 at=new Vector3(side*5.12f,deck,-4.03f+bench*3.17f);
                Box(Frame,at+Vector3.up*.88f,new Vector3(2.82f,.16f,1.36f));
                Box(Dark,at+new Vector3(-.89f,.44f,0f),new Vector3(.55f,.88f,1.16f));
                Box(Window,at+new Vector3(.42f,1.33f,-.42f),new Vector3(1.33f,.61f,.12f));
                Chair(at+new Vector3(0f,0f,1.03f),Quaternion.identity);
                Axial(Shell,at+new Vector3(-.55f,.97f,-.08f),Vector3.up,12,(0f,.24f),(.33f,.24f),(.43f,.13f));
                Box(Trim,at+new Vector3(.66f,.99f,.31f),new Vector3(.49f,.10f,.36f));
                Beam(Shell,at+new Vector3(.83f,1.02f,.26f),at+new Vector3(.83f,1.49f,.26f),.085f);
                Beam(Frame,at+new Vector3(.83f,1.49f,.26f),at+new Vector3(.59f,1.56f,.20f),.10f);
                Axial(Dark,at+new Vector3(.59f,1.32f,.20f),Vector3.up,8,(0f,.077f),(.25f,.096f));
            }
            InteriorLight(new Vector3(side*4.04f,3.78f,.1f),new Color(.67f,.84f,1f),7.9f,2.2f);
        }
        Box(Frame,new Vector3(0f,3.91f,-7.13f),new Vector3(2.82f,1.38f,.41f));
        Box(Frame,new Vector3(0f,.53f,-7.18f),new Vector3(2.82f,.18f,.41f));
        for(int section=-3;section<=3;section++)
        {
            float x=section*2.6f;
            Vector3 a=new Vector3(x-1.29f,1.2f,7.13f),b=new Vector3(x+1.29f,1.2f,7.13f);
            Pane(a,b,b+Vector3.up*2.54f,a+Vector3.up*2.54f);
            Box(Frame,a+Vector3.up*1.3f,new Vector3(.15f,2.63f,.31f));
        }
        Box(Shell,new Vector3(0f,.93f,7.06f),new Vector3(18.45f,.52f,.31f));
        Box(Frame,new Vector3(0f,3.95f,7.08f),new Vector3(18.45f,.44f,.38f));
        Box(Window,new Vector3(0f,4.2f,7.22f),new Vector3(5.64f,.103f,.054f));
        Vector3 dome = new Vector3(0f, 5.10f, -.95f);
        Axial(Frame,dome,Vector3.up,32,(0f,5.94f),(.31f,5.94f),(.45f,5.68f));
        Axial(Shell,dome,Vector3.up,32,(.39f,5.64f),(1.65f,5.44f),(2.85f,4.77f),
            (3.81f,3.74f),(4.46f,2.41f),(4.77f,1.14f),(4.86f,.02f));
        Vector3 axis = new Vector3(-.33f,.79f,.53f).normalized;
        Vector3 receiver = dome + new Vector3(-.1f,4.29f,.68f);
        Axial(Frame,receiver,axis,20,(0f,.63f),(2.62f,.65f),(3.16f,.89f),(3.32f,.89f));
        Axial(Dark,receiver,axis,20,(3.32f,.76f),(3.36f,.76f));
        Axial(Window,receiver,axis,20,(3.36f,.64f),(3.39f,.64f));
        for(int segment=0;segment<8;segment++)
        {
            float a = segment*Mathf.PI*.25f;
            Vector3 radial = new Vector3(Mathf.Cos(a),0f,Mathf.Sin(a));
            Beam(Frame,dome+radial*5.69f+Vector3.up*.52f,dome+radial*5.48f+Vector3.up*1.67f,.11f);
            Beam(Frame,dome+radial*5.48f+Vector3.up*1.67f,dome+radial*4.82f+Vector3.up*2.86f,.11f);
            Beam(Frame,dome+radial*4.82f+Vector3.up*2.86f,dome+radial*3.79f+Vector3.up*3.83f,.11f);
        }
    }

    void BuildProcessingHall(bool fuel)
    {
        Foundation(0f,0f,20.3f,16.5f);
        Box(Interior,new Vector3(0f,.57f,-1.52f),new Vector3(19.80f,.14f,12.82f));
        Box(Dark,new Vector3(0f,2.99f,4.84f),new Vector3(19.74f,4.74f,.22f));
        foreach (float side in new[]{-1f,1f})
        {
            Box(Dark,new Vector3(side*9.87f,3f,-1.52f),new Vector3(.26f,4.77f,12.82f));
            Box(Shell,new Vector3(side*5.63f,3f,-7.91f),new Vector3(8.43f,4.77f,.28f));
        }
        Box(Frame,new Vector3(0f,4.28f,-7.98f),new Vector3(2.83f,2.06f,.35f));
        Box(Frame,new Vector3(0f,.55f,-7.98f),new Vector3(2.83f,.12f,.35f));
        InteriorLight(new Vector3(0f,4.3f,-2.1f),new Color(1f,.8f,.59f),7f,2f);
        Box(Shell,new Vector3(0f,5.31f,-1.44f),new Vector3(19.82f,.53f,13.04f));
        for(int bay=-3;bay<=3;bay++)
        {
            float x=bay*2.63f;
            Box(Frame,new Vector3(x,2.71f,4.99f),new Vector3(2.46f,3.73f,.22f));
            Box(Window,new Vector3(x,3.71f,5.13f),new Vector3(1.72f,.27f,.058f));
            Box(fuel?Interior:Trim,new Vector3(x,1.2f,5.14f),new Vector3(1.65f,.20f,.082f));
            for(int slot=0;slot<4;slot++)
                Box(Dark,new Vector3(x,2.06f+slot*.20f,5.13f),new Vector3(1.64f,.071f,.049f));
        }
        if(fuel)
        {
            for(int index=0;index<3;index++)
            {
                Vector3 tank=new Vector3(-5.6f+index*5.4f,0f,.74f);
                Axial(Shell,tank,Vector3.up,24,(5.46f,1.64f),(6.02f,1.91f),(11.15f,1.91f),
                    (11.91f,1.30f),(12.15f,.50f));
                foreach(float y in new[]{6.32f,9.41f,10.58f})
                    Axial(y>10f?Window:Frame,tank,Vector3.up,24,(y,1.97f),(y+.21f,1.97f));
                Pipe(tank+new Vector3(0f,11.99f,.2f),tank+new Vector3(0f,12.64f,.2f),.22f);
                ServiceLadder(tank+new Vector3(1.6f,5.58f,1.14f),11.65f);
                Equipment(new Vector3(tank.x,.64f,6.5f),3.45f,2.92f);
            }
            Pipe(new Vector3(-5.6f,12.54f,.95f),new Vector3(5.2f,12.54f,.95f),.23f);
        }
        else
        {
            for(int index=0;index<3;index++)
            {
                Vector3 throat=new Vector3(-5.73f+index*5.62f,5.45f,-1.1f);
                Axial(Frame,throat,Vector3.up,8,(0f,.79f),(1.6f,1.16f),(3.16f,2.17f),(3.52f,2.17f));
                Axial(Dark,throat,Vector3.up,8,(3.54f,1.98f),(3.58f,1.98f));
                Equipment(new Vector3(throat.x,.64f,6.44f),4.25f,2.87f);
                Beam(Trim,throat+new Vector3(1.6f,2.94f,1.45f),
                    throat+new Vector3(1.08f,.1f,1.85f),.16f);
            }
            Pipe(new Vector3(-7.08f,6.2f,1.1f),new Vector3(6.58f,6.2f,1.1f),.27f);
            ServiceRail(new Vector3(-9.19f,5.61f,4.49f),new Vector3(8.93f,5.61f,4.49f),12);
        }
        Box(Frame,new Vector3(0f,1.89f,5.24f),new Vector3(2.5f,2.61f,.42f));
        Box(Dark,new Vector3(0f,1.83f,5.48f),new Vector3(1.64f,2.37f,.065f));
        Box(Window,new Vector3(0f,2.57f,5.51f),new Vector3(1.05f,.24f,.062f));
        foreach(int side in new[]{-1,1})
            Axial(fuel?Window:Trim,new Vector3(side*9.97f,.86f,side>0?2.9f:1.9f),Vector3.right,10,
                (-.16f,.35f),(.16f,.35f));
    }

    void BuildSolarStation()
    {
        Foundation(0f, 12.9f, 6.8f, 3.05f);
        Foundation(0f, -.45f, 1.85f, 22.3f);
        Quaternion tilt = Quaternion.Euler(21f, 0f, 0f);
        for (int row = -1; row <= 1; row++)
        for (int column = -1; column <= 1; column += 2)
        {
            Vector3 pivot = new Vector3(column * 5.73f, 4.20f, row * 8.17f - .62f + column * .57f);
            Vector3 P(float x, float y, float z) => pivot + tilt * new Vector3(x, y, z);
            Box(Dark, P(0f, -.14f, 0f), new Vector3(9.57f, .23f, 6.53f), tilt);
            for (int i = -1; i <= 1; i += 2)
            {
                Box(Frame, P(i * 4.77f, .025f, 0f), new Vector3(.12f, .16f, 6.55f), tilt);
                Box(Frame, P(0f, .025f, i * 3.19f), new Vector3(9.57f, .16f, .13f), tilt);
                Foundation(pivot.x + i * 2.89f, pivot.z, 1.36f, 1.61f);
                Box(Frame, new Vector3(pivot.x + i * 2.89f, .75f, pivot.z), new Vector3(.62f, .20f, .86f));
                Beam(Frame, new Vector3(pivot.x + i * 2.89f, .82f, pivot.z), P(i * 2.89f, -.21f, 0f), .22f);
                Beam(Dark, new Vector3(pivot.x + i * 2.89f, 1.60f, pivot.z), P(i * 2.89f, -.24f, 1.34f), .135f);
            }
            for (int x = 0; x < 12; x++)
            for (int z = 0; z < 9; z++)
                Box(Seat, P((x - 5.5f) * .788f, -.003f, (z - 4f) * .710f),
                    new Vector3(.745f, .041f, .676f), tilt);
            Beam(Frame, P(-4.48f, -.24f, 0f), P(4.48f, -.24f, 0f), .19f);
            Beam(Dark, new Vector3(pivot.x, .69f, pivot.z), new Vector3(column * .60f, .69f, pivot.z), .1f);
        }
        for (int side = -1; side <= 1; side += 2)
            Beam(Dark, new Vector3(side * .60f, .69f, -9.6f), new Vector3(side * .60f, .69f, 13.5f), .13f);
        Equipment(new Vector3(-1.55f, .64f, 12.7f), 2.18f, 2.05f);
        Equipment(new Vector3(1.56f, .64f, 12.7f), 2.34f, 2.05f);
        Box(Dark, new Vector3(0f, 1.56f, 13.04f), new Vector3(1.56f, 1.78f, 2.67f));
        Box(Frame, new Vector3(0f, 1.57f, 14.40f), new Vector3(1.48f, 1.61f, .13f));
        Box(Window, new Vector3(0f, 1.95f, 14.49f), new Vector3(.57f, .23f, .046f));
        Box(Trim, new Vector3(0f, 1.12f, 14.51f), new Vector3(.43f, .19f, .14f));
        for (int lamp = -1; lamp <= 1; lamp++)
            Box(Beacon, new Vector3(lamp * .36f + 1.55f, 1.60f, 14.27f), new Vector3(.15f, .10f, .08f));
    }

    // Rear and side bulkheads leave a clear throat exactly matching the pressure passage.
    void IndustrialHull(float height, string[] stencil, bool frontReadable = false)
    {
        Foundation(0f, -1.25f, 18.2f, 17.5f);
        const float portY = 2.35f;
        const float baseY = .62f;
        const float top = portY + PressureCorridor.Roof;
        const float bottom = portY + PressureCorridor.Floor;
        Box(Frame, new Vector3(0f, height + .18f, -6f), new Vector3(18.14f, .36f, 8.14f));
        Box(Interior, new Vector3(0f, (baseY + bottom) * .5f, -6f), new Vector3(18f, bottom - baseY, 8f));
        Bulkhead(new Vector3(0f, 0f, -10f), Quaternion.Euler(0f, 180f, 0f), 18f);
        foreach (float side in new[] { -1f, 1f })
            Box(Shell, new Vector3(side * 8.86f, (height + baseY) * .5f, -6f),
                new Vector3(.28f, height - baseY, 8f));
        Box(Shell, new Vector3(0f, (height + baseY) * .5f, -2.04f), new Vector3(18f, height - baseY, .25f));
        Box(Frame, new Vector3(0f, 1.99f, -1.79f), new Vector3(2.52f, 2.68f, .63f));
        Box(Dark, new Vector3(0f, 1.91f, -1.45f), new Vector3(1.99f, 2.41f, .065f));
        Box(Window, new Vector3(0f, 2.52f, -1.406f), new Vector3(.86f, .33f, .04f));
        Box(Trim, new Vector3(0f, .68f, -.94f), new Vector3(2.68f, .19f, 1.24f));
        for (int side = -1; side <= 1; side += 2)
        {
            Box(Window, new Vector3(side * 2.38f, 3.39f, -1.895f), new Vector3(1.82f, .55f, .045f));
            Box(Beacon, new Vector3(side * 1.36f, 2.94f, -1.42f), new Vector3(.16f, .24f, .09f));
            Equipment(new Vector3(side * 6.1f, height + .37f, -7.5f), 3.15f, 2.34f);
            for (int rail = 0; rail < 4; rail++)
                Box(Frame, new Vector3(side * (3.68f + rail * 1.35f), height * .52f, -1.89f),
                    new Vector3(.085f, height - .93f, .11f));
        }
        Sign(new Vector3(0f, height - .92f, -1.876f), stencil, .98f, frontReadable);

        void Bulkhead(Vector3 origin, Quaternion rotation, float width)
        {
            void Panel(float x, float low, float high, float span, int material)
            {
                Box(material, origin + rotation * new Vector3(x, (low + high) * .5f, -.14f),
                    new Vector3(span, high - low, .28f), rotation);
            }
            float half = PressureCorridor.HalfWidth;
            float wing = width * .5f - half;
            Panel(-half - wing * .5f, bottom, top, wing, Shell);
            Panel(half + wing * .5f, bottom, top, wing, Shell);
            Panel(0f, baseY, bottom, width, Frame);
            Panel(0f, top, height, width, Shell);
            for (int side = -1; side <= 1; side += 2)
                Box(Frame, origin + rotation * new Vector3(side * (half + .07f), portY + .165f, -.06f),
                    new Vector3(.14f, top - bottom, .12f), rotation);
        }
    }

    void Sign(Vector3 origin, string[] stencil, float height, bool frontReadable)
    {
        float pitch = height / stencil.Length;
        for (int row = 0; row < stencil.Length; row++)
        for (int col = 0; col < stencil[row].Length; col++)
        {
            if (stencil[row][col] != '#') continue;
            Box(Window, origin + new Vector3((col - (stencil[row].Length - 1) * .5f) * pitch * (frontReadable ? -1f : 1f),
                ((stencil.Length - 1) * .5f - row) * pitch, 0f), new Vector3(pitch * 1.005f, pitch * 1.005f, .023f));
        }
    }

    void BuildWaterStation()
    {
        InteriorLight(new Vector3(0f, 2.8f, -5.7f), new Color(.77f, .87f, 1f), 2.15f, .85f);
        IndustrialHull(5.75f, new[] { "# #   ##   ### ", "# #     #  #  #", "###   ##   #  #", "# #   #    #  #", "# #   ###  ### " });
        Vector3 tower = new Vector3(-4.4f, 0f, 2.74f);
        Axial(Frame, tower, Vector3.up, 24, (10.5f, 1.42f), (11.17f, 2.95f), (11.83f, 3.51f));
        Axial(Shell, tower, Vector3.up, 32, (11.76f, 3.49f), (12.34f, 3.88f), (15.36f, 3.88f),
            (16.14f, 3.32f), (16.64f, 1.59f));
        foreach (float ring in new[] { 12.68f, 14.95f })
            Axial(Frame, tower, Vector3.up, 32, (ring, 3.94f), (ring + .17f, 3.94f));
        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 foot = tower + new Vector3(x * 2.85f, .64f, z * 2.68f);
            Vector3 head = tower + new Vector3(x * 1.87f, 11.33f, z * 1.87f);
            Box(Dark, foot + Vector3.up * .23f, new Vector3(1.26f, .46f, 1.26f));
            Beam(Frame, foot, head, .53f);
            Beam(Frame, foot + Vector3.up * 1.38f, Vector3.Lerp(foot, head, .68f) + Vector3.right * -x * 3.8f, .29f);
            Box(Beacon, head + Vector3.up * .18f, new Vector3(.36f, .18f, .36f));
        }
        Pipe(tower + new Vector3(0f, 1.17f, 0f), tower + new Vector3(0f, 11.8f, 0f), .44f);
        Equipment(tower + new Vector3(0f, .64f, 0f), 2.8f, 3.2f);
        Vector3 drill = new Vector3(5.5f, 0f, 1.6f);
        Drill(drill, 9.7f, .47f);
        for (int index = 0; index < 2; index++)
        {
            Vector3 tank = new Vector3(3.65f + index * 3.05f, 0f, 5.25f);
            Axial(Shell, tank, Vector3.up, 20, (.64f, .83f), (1.05f, 1.08f), (3.16f, 1.08f), (3.55f, .68f));
            Axial(Frame, tank, Vector3.up, 20, (1.32f, 1.12f), (1.48f, 1.12f));
            Pipe(tank + Vector3.up * 3.5f, tank + Vector3.up * 4.17f, .21f);
            Pump(tank + new Vector3(0f, 1.04f, 1.68f));
            Box(Window, tank + new Vector3(0f, 2.48f, 1.077f), new Vector3(.37f, .16f, .07f));
        }
    }

    void BuildOxygenStation()
    {
        InteriorLight(new Vector3(0f, 2.8f, -5.7f), new Color(.77f, .87f, 1f), 2.15f, .85f);
        IndustrialHull(8.05f, new[] { " ###       ", "#   #      ", "#   #      ", "#   #   ## ", "#   #     #", " ###    ## ", "       ### " }, true);
        Vector3 reactor = new Vector3(-5.18f, 0f, 2.8f);
        Axial(Dark, reactor, Vector3.up, 16, (.62f, 2.66f), (1.46f, 2.66f));
        Axial(Frame, reactor, Vector3.up, 24, (1.4f, 2.3f), (6.24f, 2.3f), (7.04f, 1.89f), (7.54f, .81f));
        Axial(Shell, reactor, Vector3.up, 20, (7.49f, .84f), (7.94f, .84f), (8.09f, .7f));
        for (int band = 0; band < 4; band++)
            Axial(band == 2 ? Trim : Shell, reactor, Vector3.up, 24,
                (1.91f + band * 1.04f, 2.38f), (2.15f + band * 1.04f, 2.38f));
        Equipment(new Vector3(-5.16f, .64f, 6.03f), 4.45f, 2.38f);
        Pipe(reactor + new Vector3(0f, 7.98f, 0f), new Vector3(-5.18f, 9.26f, 2.8f), .28f);
        Pipe(new Vector3(-5.18f, 9.26f, 2.8f), new Vector3(-5.18f, 9.26f, -.55f), .28f);

        for (int index = 0; index < 3; index++)
        {
            float x = 1.34f + 2.93f * index;
            float z = 2.56f + .45f * (index % 2);
            Vector3 tank = new Vector3(x, 0f, z);
            float top = index == 1 ? 15.3f : 14.3f;
            Axial(Frame, tank, Vector3.up, 20, (.62f, 1.39f), (1.22f, 1.39f));
            Axial(Shell, tank, Vector3.up, 28, (1.23f, 1.25f), (top - 1.36f, 1.25f),
                (top - .91f, 1.17f), (top - .49f, .98f), (top - .19f, .70f), (top, .25f));
            foreach (float seam in new[] { 2.56f, 6.12f, 9.58f, top - 1.38f })
                Axial(seam == 9.58f ? Window : Frame, tank, Vector3.up, 28, (seam, 1.265f), (seam + .12f, 1.265f));
            Pipe(tank + Vector3.up * (top - .02f), tank + Vector3.up * (top + .44f), .162f);
            Axial(Beacon, tank, Vector3.up, 12, (top + .44f, .21f), (top + .56f, .19f));
            Pipe(new Vector3(x, 5.1f, z + 1.24f), new Vector3(x, 5.1f, 5.65f), .19f);
            Pipe(new Vector3(x, 5.1f, 5.65f), new Vector3(x, 6.18f, 5.65f), .19f);
            Axial(Window, new Vector3(x, 6.18f, 5.65f), Vector3.up, 10, (0f, .29f), (.10f, .29f));
            Box(Dark, new Vector3(x, 8.17f, z + 1.24f), new Vector3(.77f, .64f, .19f));
            Box(Window, new Vector3(x, 8.24f, z + 1.35f), new Vector3(.40f, .15f, .04f));
            Pump(new Vector3(x, 1.18f, 6.16f));
            Box(Frame, new Vector3(x, 2.85f, 6.57f), new Vector3(.19f, 4.4f, .24f));
        }
        Pipe(new Vector3(-5.18f, 5.1f, 5.65f), new Vector3(7.54f, 5.1f, 5.65f), .26f);
        Box(Frame, new Vector3(4.07f, 6.83f, 4.94f), new Vector3(9.18f, .22f, 1.54f));
        ServiceRail(new Vector3(-.52f, 6.94f, 5.72f), new Vector3(8.66f, 6.94f, 5.72f), 8);
        ServiceLadder(new Vector3(8.57f, .67f, 5.72f), 7.19f);
        Equipment(new Vector3(4.59f, .64f, 6.63f), 6.55f, 1.58f);
        BuildCoolingEnclosure();
    }

    static Vector3 FanCenter(int index) => new Vector3(10.34f,3.10f,-7.90f+index*3.82f);

    void BuildCoolingEnclosure()
    {
        Foundation(9.6f,-5.9f,2.1f,8.8f);
        Box(Dark,new Vector3(9.58f,2.91f,-5.99f),new Vector3(1.37f,4.53f,8.38f));
        Box(Frame,new Vector3(9.85f,5.21f,-5.99f),new Vector3(1.88f,.24f,8.31f));
        Box(Dark, new Vector3(10.07f, 5.66f, -5.99f), new Vector3(.32f, .73f, 2.38f));
        Vector3 Letter(float y, float z) => new Vector3(10.26f, 5.66f+y, -5.99f+z);
        var oxygen = new[] { Letter(-.20f,-.24f), Letter(-.27f,-.46f), Letter(-.20f,-.68f),
            Letter(.20f,-.68f), Letter(.27f,-.46f), Letter(.20f,-.24f) };
        for (int edge=0;edge<oxygen.Length;edge++)
            Beam(Window,oxygen[edge],oxygen[(edge+1)%oxygen.Length],.065f);
        var subscript = new[] { Letter(.01f,-.12f),Letter(.01f,.12f),Letter(-.12f,.12f),
            Letter(-.12f,-.12f),Letter(-.26f,-.12f),Letter(-.26f,.12f) };
        for (int edge=1;edge<subscript.Length;edge++)
            Beam(Window,subscript[edge-1],subscript[edge],.049f);
        for(int i=0;i<2;i++)
        {
            Vector3 fan=FanCenter(i);
            const int count=36;
            for(int blade=0;blade<count;blade++)
            {
                float a=blade*Mathf.PI*2f/count, b=(blade+1)*Mathf.PI*2f/count;
                Vector3 Radial(float angle,float radius,float depth)=>fan+new Vector3(depth,
                    Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius);
                Vector3 p=Radial(a,1.58f,.28f), q=Radial(b,1.58f,.28f);
                Vector3 r=Radial(b,1.88f,.28f), s=Radial(a,1.88f,.28f);
                Quad(Frame,p,q,r,s);
                Quad(Frame,s,r,q,p);
                Quad(Dark,s,r,r-Vector3.right*.41f,s-Vector3.right*.41f);
                Quad(Dark,q,p,p-Vector3.right*.34f,q-Vector3.right*.34f);
                if (blade%9==0)
                    Box(Trim,Radial(a,1.73f,.310f),new Vector3(.061f,.18f,.19f));
            }
            foreach(float halo in new[]{.55f,1.02f,1.49f})
            for(int arc=0;arc<30;arc++)
            {
                float a=arc*Mathf.PI*2f/30f, b=(arc+1)*Mathf.PI*2f/30f;
                Beam(Frame,fan+new Vector3(.33f,Mathf.Cos(a)*halo,Mathf.Sin(a)*halo),
                    fan+new Vector3(.33f,Mathf.Cos(b)*halo,Mathf.Sin(b)*halo),.037f);
            }
            for(int brace=0;brace<8;brace++)
            {
                float theta=brace*Mathf.PI/4f;
                Beam(Frame,fan+Vector3.right*.34f,fan+new Vector3(.34f,Mathf.Cos(theta)*1.59f,
                    Mathf.Sin(theta)*1.59f),.066f);
            }
            Box(Window,fan+new Vector3(.31f,-2.04f,.49f),new Vector3(.057f,.15f,.66f));
            Pipe(fan+new Vector3(-.9f,2.25f,0f),fan+new Vector3(-.9f,2.46f,0f),.17f);
            Pipe(fan+new Vector3(-.9f,2.46f,0f),fan+new Vector3(-4.34f,2.46f,0f),.17f);
        }
    }

    void BuildCoolingRotors()
    {
        for(int fan=0;fan<coolingRotors.Length;fan++)
            coolingRotors[fan]=Machinery("Sealed equipment cooling fan",FanCenter(fan), () =>
            {
                Axial(Frame,Vector3.zero,Vector3.right,18,(-.16f,.43f),(.05f,.39f),(.20f,.26f));
                for(int blade=0;blade<9;blade++)
                {
                    Quaternion turn=Quaternion.Euler(blade*40f,0f,0f);
                    Vector3 a=turn*new Vector3(.06f,.34f,-.145f);
                    Vector3 b=turn*new Vector3(-.19f,1.45f,.20f);
                    Vector3 c=turn*new Vector3(.005f,1.41f,.52f);
                    Vector3 d=turn*new Vector3(.17f,.47f,.195f);
                    Quad(Shell,a,b,c,d);
                    Quad(Frame,d,c,b,a);
                }
            });
    }

    static readonly Vector3 AugerCenter = new Vector3(4.75f, 0f, 5.20f);
    static readonly Vector3 BeltIntake = new Vector3(2.68f, 1.31f, 7.12f);
    static readonly Vector3 BeltDischarge = new Vector3(-5.44f, 4.15f, .86f);
    Transform drillRod, feedBelt;
    readonly List<Mesh> machineryMeshes = new List<Mesh>();

    void BuildMaterialsStation()
    {
        IndustrialHull(7.05f, new[] { "# #   #   # ###", "###   ##  # #  ", "# #   # # # ###", "# #   #  ## #  ", "# #   #   # ###" }, true);
        Foundation(4.75f, 8.15f, 7.4f, 5.8f);
        Axial(Dark, AugerCenter, Vector3.up, 24, (.61f, 1.75f), (.84f, 1.75f), (1.00f, 1.43f));
        Axial(Frame, AugerCenter, Vector3.up, 24, (8.55f, 1.65f), (8.83f, 1.77f), (10.34f, 1.77f), (10.70f, 1.39f));
        Axial(Dark, AugerCenter, Vector3.up, 24, (10.58f, 1.33f), (13.72f, 1.33f), (14.12f, .93f));
        Box(Frame, AugerCenter + Vector3.up * 15.85f, new Vector3(4.12f, .46f, 3.56f));
        Box(Dark, AugerCenter + Vector3.up * 16.15f, new Vector3(1.85f, .38f, 1.86f));
        Box(Beacon, AugerCenter + Vector3.up * 16.46f, new Vector3(.35f, .25f, .33f));

        for (int frame = 0; frame < 4; frame++)
        {
            Quaternion yaw = Quaternion.Euler(0f, frame * 90f, 0f);
            Vector3 P(float x, float y, float z) => AugerCenter + yaw * new Vector3(x,y,z);
            Beam(Frame, P(-2.22f,.65f,2.12f), P(-1.43f,15.82f,1.48f), .32f);
            for (int level = 0; level < 5; level++)
            {
                float y = 1.15f + level * 2.84f;
                float low = Mathf.Lerp(2.2f,1.43f,y/16f), high = Mathf.Lerp(2.2f,1.43f,(y+2.84f)/16f);
                Beam(Frame,P(-low,y,low),P(low,y,low),.16f);
                if (frame != 0 || level > 2)
                    Beam(Trim,P(-low,y,low),P(high,y+2.84f,high),.15f);
            }
            Vector3 deck = P(0f,8.63f,2.03f);
            Box(Frame,deck,new Vector3(4.58f,.21f,.91f),yaw);
            ServiceRail(P(-2.28f,8.75f,2.43f),P(2.28f,8.75f,2.43f),5);
            ServiceRail(P(-1.94f,16.04f,1.7f),P(1.94f,16.04f,1.7f),4);
        }
        ServiceLadder(AugerCenter + new Vector3(1.94f,.62f,2.53f), 8.85f);
        ServiceLadder(AugerCenter + new Vector3(1.51f,8.77f,2.45f), 16.12f);
        Equipment(AugerCenter + new Vector3(2.66f,.64f,.34f),1.53f,3.05f);
        for (int fin=-3;fin<=3;fin++)
            Box(Trim,AugerCenter+new Vector3(1.56f,11.55f+fin*.24f,0f),new Vector3(.42f,.11f,1.74f));
        Pipe(AugerCenter+new Vector3(-1.28f,10.48f,0f),AugerCenter+new Vector3(-2.22f,10.48f,0f),.14f);
        Pipe(AugerCenter+new Vector3(-2.22f,10.48f,0f),AugerCenter+new Vector3(-2.22f,1.46f,0f),.14f);

        Vector3 belt = BeltDischarge - BeltIntake;
        Vector3 across = Vector3.Cross(Vector3.up, belt.normalized).normalized;
        Beam(Dark,BeltIntake,BeltDischarge,1.36f);
        foreach (float side in new[] { -.75f, .75f })
        {
            Beam(Frame,BeltIntake+across*side+Vector3.up*.34f,BeltDischarge+across*side+Vector3.up*.34f,.18f);
            Beam(Trim,BeltIntake+across*side+Vector3.up*.50f,BeltDischarge+across*side+Vector3.up*.50f,.065f);
            for (int support=1;support<=4;support++)
            {
                Vector3 top=Vector3.Lerp(BeltIntake,BeltDischarge,support/5f)+across*side;
                Beam(Frame,new Vector3(top.x,.62f,top.z),top,.17f);
            }
        }
        Box(Frame,BeltIntake+Vector3.up*.37f,new Vector3(1.62f,.54f,1.63f));
        Box(Dark,BeltDischarge+Vector3.down*.67f,new Vector3(2.1f,1.27f,2.24f));
        Equipment(new Vector3(-5.63f,.64f,.67f),3.57f,3.70f);
        for (int crate=0;crate<3;crate++)
        {
            Vector3 point=new Vector3(-6.13f+crate*2.14f,1.33f,6.42f);
            Box(Dark,point,new Vector3(1.98f,1.35f,2.56f));
            Box(Frame,point+Vector3.up*.71f,new Vector3(2.02f,.13f,2.62f));
            for(int rib=-2;rib<=2;rib++)
                Box(Trim,point+new Vector3(rib*.35f,0f,1.30f),new Vector3(.091f,1.26f,.083f));
        }
        Box(Frame,new Vector3(-6.83f,2.79f,-.13f),new Vector3(.59f,2.58f,2.36f));
        Box(Window,new Vector3(-6.56f,3.17f,1.08f),new Vector3(.38f,.54f,.075f));
        ServiceRail(new Vector3(-8.26f,.65f,7.84f),new Vector3(-.91f,.65f,7.84f),7);
    }

    void BuildMovingMine()
    {
        drillRod = Machinery("Vertical spiral regolith auger", AugerCenter, () =>
        {
            Axial(Frame,Vector3.zero,Vector3.up,24,(.20f,.13f),(.78f,.43f),(8.94f,.43f),(10.15f,.52f));
            const int sections = 214;
            for(int i=0;i<sections;i++)
            {
                float a=i/(float)sections, b=(i+1f)/sections;
                Vector3 innerA=Edge(a,false), outerA=Edge(a,true);
                Vector3 innerB=Edge(b,false), outerB=Edge(b,true);
                Vector3 thickness=Vector3.up*.10f;
                Quad(Shell,innerA,innerB,outerB,outerA);
                Quad(Dark,innerA-thickness,outerA-thickness,outerB-thickness,innerB-thickness);
                Quad(Trim,outerA,outerB,outerB-thickness,outerA-thickness);
            }

            static Vector3 Edge(float t,bool outer)
            {
                float angle=t*Mathf.PI*2f*6.85f;
                float radius=outer?Mathf.Lerp(.30f,1.43f,Mathf.Min(t*6.1f,1f)):.36f;
                return new Vector3(Mathf.Cos(angle)*radius,.31f+t*8.18f,Mathf.Sin(angle)*radius);
            }
        });
        Vector3 lift=BeltDischarge-BeltIntake;
        Vector3 across=Vector3.Cross(Vector3.up,lift.normalized).normalized;
        feedBelt = Machinery("Regolith transfer belt",BeltIntake,()=>{
            for(int cleat=1;cleat<=24;cleat++)
            {
                Vector3 center=lift*(cleat/26f)+Vector3.up*.69f;
                Beam(Frame,center-across*.60f,center+across*.60f,.067f);
                if (cleat%3!=0)
                    Box(Seat,center+across*Mathf.Sin(cleat*2.5f)*.37f+Vector3.up*.12f,
                        new Vector3(.29f,.19f,.25f),Quaternion.Euler(11f,cleat*44f,8f));
            }
        });
    }

    Transform Machinery(string label, Vector3 pivot, System.Action build)
    {
        build();
        var partMesh = new Mesh { name = label, subMeshCount = faces.Length };
        partMesh.SetVertices(vertices);
        for (int material = 0; material < faces.Length; material++) partMesh.SetTriangles(faces[material], material);
        partMesh.RecalculateNormals();
        partMesh.RecalculateBounds();
        vertices.Clear();
        foreach (List<int> face in faces) face.Clear();
        machineryMeshes.Add(partMesh);
        var part = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
        part.transform.SetParent(transform, false);
        part.transform.localPosition = pivot;
        part.GetComponent<MeshFilter>().sharedMesh = partMesh;
        part.GetComponent<MeshRenderer>().sharedMaterials = materials;
        return part.transform;
    }

    bool CanAnimate() => powered && (drillRod!=null || waterBit!=null || coolingRotors[0]!=null ||
        (processRotor!=null && processing));

    internal void SetProcessing(bool running)
    {
        if(processing==running) return;
        processing=running;
        enabled=CanAnimate();
    }

    void LateUpdate()
    {
        operatingTime += Time.deltaTime;
        if(processRotor!=null) processRotor.localRotation=Quaternion.Euler(0,0,operatingTime*119f);
        if (drillRod != null)
        {
            drillRod.localRotation = Quaternion.Euler(0f, operatingTime * 97f, 0f);
            drillRod.localPosition = AugerCenter + Vector3.up * (.12f * Mathf.Sin(operatingTime * 1.2f));
            feedBelt.localPosition = BeltIntake + (BeltDischarge - BeltIntake) *
                (Mathf.Repeat(operatingTime * 1.14f, 1f) / 26f);
        }
        for (int i = 0; i < coolingRotors.Length; i++)
            if (coolingRotors[i] != null)
                coolingRotors[i].localRotation = Quaternion.Euler(operatingTime * (i == 0 ? 333f : -307f), 0f, 0f);
        if (waterBit != null)
        {
            waterBit.localPosition = new Vector3(5.5f, .16f * Mathf.Sin(operatingTime * 1.45f), 1.6f);
            waterBit.localRotation = Quaternion.Euler(0f, operatingTime * 112f, 0f);
        }
    }

    void ServiceLadder(Vector3 origin, float top)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 foot = origin + Vector3.right * (side * .37f);
            Beam(Frame, foot, new Vector3(foot.x, top + .60f, foot.z), .11f);
        }
        for (float y = origin.y + .2f; y <= top; y += .32f)
            Box(Frame, new Vector3(origin.x, y, origin.z), new Vector3(.74f, .069f, .085f));
    }

    void ServiceRail(Vector3 from, Vector3 to, int spans)
    {
        for (int index = 0; index <= spans; index++)
        {
            Vector3 foot = Vector3.Lerp(from, to, index / (float)spans);
            Beam(Frame, foot, foot + Vector3.up * .94f, .087f);
        }
        foreach (float rail in new[] { .49f, .94f })
            Beam(Frame, from + Vector3.up * rail, to + Vector3.up * rail, .077f);
    }

    void Drill(Vector3 center, float height, float radius)
    {
        Axial(Dark, center, Vector3.up, 16, (.60f, radius * 1.68f), (.88f, radius * 1.68f), (1.24f, radius * 1.44f));
        if (kind != BuildingKind.WaterStation)
            Axial(Frame, center, Vector3.up, 16, (-.38f, radius * .66f), (1.15f, radius), (height - 1.44f, radius * .64f));
        Axial(Trim, center, Vector3.up, 14, (height - 3.77f, radius * 1.27f), (height - 2.14f, radius * 1.27f));
        const float baseSize = 1.31f;
        for (int side = 0; side < 4; side++)
        {
            Quaternion rotation = Quaternion.Euler(0f, side * 90f, 0f);
            Vector3 P(float x, float y, float z) => center + rotation * new Vector3(x, y, z);
            Beam(Frame, P(-baseSize, .62f, baseSize), P(-baseSize * .65f, height, baseSize * .65f), .31f);
            const int levels = 3;
            for (int level = 0; level < levels; level++)
            {
                float low = Mathf.Lerp(.76f, height - .36f, level / (float)levels);
                float high = Mathf.Lerp(.76f, height - .36f, (level + 1f) / levels);
                float a = Mathf.Lerp(baseSize, baseSize * .65f, low / height);
                float b = Mathf.Lerp(baseSize, baseSize * .65f, high / height);
                Beam(Frame, P(-a, low, a), P(a, low, a), .17f);
                Beam(Trim, P(-a, low, a), P(b, high, b), .16f);
            }
        }
        float deck = height * .51f;
        float edge = baseSize * .91f;
        for (int side = -1; side <= 1; side += 2)
        {
            Box(Frame, center + new Vector3(side * .91f, deck, edge), new Vector3(1.25f, .15f, 1.02f));
            Beam(Frame, center + new Vector3(side * 1.5f, deck + .91f, edge + .48f),
                center + new Vector3(side * .32f, deck + .91f, edge + .48f), .065f);
            Beam(Frame, center + new Vector3(side * 1.5f, deck, edge + .48f),
                center + new Vector3(side * 1.5f, deck + .91f, edge + .48f), .085f);
            Beam(Frame, center + new Vector3(side * .32f, .7f, edge + .52f),
                center + new Vector3(side * .32f, deck + .95f, edge + .52f), .073f);
        }
        for (float rung = .94f; rung < deck + .62f; rung += .31f)
            Box(Frame, center + new Vector3(0f, rung, edge + .52f), new Vector3(.68f, .058f, .075f));
        Box(Frame, center + Vector3.up * height, new Vector3(baseSize * 1.5f, .37f, baseSize * 1.5f));
        Box(Beacon, center + Vector3.up * (height + .29f), new Vector3(.38f, .24f, .38f));
    }

    void BuildGreenhouse()
    {
        Foundation(0f, -.4f, 13.9f, 20.8f);
        Box(Frame, new Vector3(0f, .61f, -.4f), new Vector3(14f, .22f, 20.9f));
        const int arc = 20;
        Vector3 Arch(int index, float z)
        {
            float angle = index * Mathf.PI / arc;
            return new Vector3(6.78f * Mathf.Cos(angle), 2.13f + 5.3f * Mathf.Sin(angle), z);
        }
        for (int bay = 0; bay < 5; bay++)
        for (int section = 0; section < arc; section++)
        {
            float near = -10.73f + bay * 4.12f;
            float far = near + 4.12f;
            Vector3 a = Arch(section, near), b = Arch(section + 1, near);
            Vector3 c = Arch(section + 1, far), d = Arch(section, far);
            if (section < 2 || section > 17 || (section >= 9 && section <= 10))
                Quad(Shell, a, b, c, d);
            else Pane(a, b, c, d);
            Beam(Frame, a, b, .15f);
            if (bay == 4) Beam(Frame, d, c, .15f);
            if (section % 5 == 0) Beam(Frame, a, d, .095f);
        }
        for (int segment = 0; segment < arc; segment++)
        {
            Vector3 a = Arch(segment, 9.87f), b = Arch(segment + 1, 9.87f);
            Vector3 c = Arch(segment, -10.73f), d = Arch(segment + 1, -10.73f);
            if (segment >= 7 && segment <= 12) Triangle(Shell, new Vector3(0f, 3.35f, 9.87f), a, b);
            else
            {
                glassVertices.Add(new Vector3(0f, 2.13f, 9.87f));
                glassVertices.Add(a);
                glassVertices.Add(b);
            }
            Triangle(Shell, new Vector3(0f, 2.13f, -10.73f), d, c);
        }
        foreach (float x in new[] { -6.82f, 6.82f })
            Box(Dark, new Vector3(x, 1.36f, -.43f), new Vector3(.25f, 1.5f, 20.6f));
        Box(Frame, new Vector3(0f, 1.29f, -10.65f), new Vector3(13.5f, 1.68f, .28f));
        foreach(float side in new[]{-1f,1f})
            Box(Frame,new Vector3(side*4.1f,1.29f,9.82f),new Vector3(5.3f,1.68f,.28f));
        Entry(9.58f, 12.98f, 1.88f);
        for (int row = -2; row <= 2; row++)
        {
            if (row == 0) continue;
            float x = row * 2.18f;
            Tray(x, 1.39f, 1.66f, 17.18f);
            bool rack = Mathf.Abs(row) == 1;
            if (rack)
            {
                Tray(x, 2.48f, 1.58f, 16.75f);
                foreach (float z in new[] { -8.4f, -4.1f, .2f, 4.5f, 7.76f })
                foreach (float side in new[] { -.77f, .77f })
                    Box(Frame, new Vector3(x + side, 1.70f, z), new Vector3(.10f, 2.17f, .10f));
                Box(Window, new Vector3(x, 2.37f, -.31f), new Vector3(.20f, .036f, 16.48f));
            }
            for (int cell = 0; cell < 19; cell++)
            {
                float z = -8.05f + cell * .86f;
                for (int lane = -1; lane <= 1; lane++)
                {
                    int seed = cell * 7 + lane * 3 + row * 13;
                    Crop(new Vector3(x + lane * .47f, 1.47f, z + lane * .12f), seed,
                        rack ? .69f : (lane == 0 ? 3.33f : 2.44f));
                    if (rack)
                        Crop(new Vector3(x + lane * .47f, 2.56f, z + .11f), seed + 47,
                            lane == 0 ? 3.55f : 2.45f);
                }
            }
            Pipe(new Vector3(x + .72f, 1.21f, -8.84f), new Vector3(x + .72f, 1.21f, 8.21f), .069f);
            if (rack)
            {
                for (int post = -2; post <= 2; post++)
                    Box(Frame, new Vector3(x, 4.1f, post * 3.78f), new Vector3(.09f, 3.14f, .09f));
                Box(Frame, new Vector3(x, 5.68f, -.31f), new Vector3(.12f, .085f, 16.32f));
            }
            if (rack)
                Pipe(new Vector3(x + .72f, 2.32f, -8.70f), new Vector3(x + .72f, 2.32f, 7.82f), .055f);
            float canopyLight = rack ? 6.29f : 5.26f;
            Box(Frame, new Vector3(x, canopyLight, -.28f), new Vector3(.40f, .12f, 15.78f));
            Box(Window, new Vector3(x, canopyLight - .07f, -.28f), new Vector3(.22f, .05f, 15.63f));
        }

        void Tray(float x, float y, float width, float length)
        {
            Box(Frame, new Vector3(x, y - .11f, -.31f), new Vector3(width, .22f, length));
            Box(Dark, new Vector3(x, y + .02f, -.31f), new Vector3(width - .16f, .064f, length - .23f));
            for (int bay = -4; bay <= 4; bay++)
                Box(Dark, new Vector3(x, y - .35f, bay * 1.91f), new Vector3(width * .71f, .49f, .29f));
        }
        Axial(Frame, new Vector3(0f, 0f, -8.5f), Vector3.up, 16, (.64f, 1.08f), (2.78f, 1.08f));
        Equipment(new Vector3(0f, .66f, -5.51f), 1.53f, 1.42f);
        Pipe(new Vector3(0f, 1.37f, -8.73f), new Vector3(4.95f, 1.37f, -8.73f), .095f);
        InteriorLight(new Vector3(0f, 4.18f, -.7f), new Color(.72f, 1f, .49f), 9.8f, 17f);
    }

    void Crop(Vector3 root, int seed, float scale)
    {
        int type = Mathf.Abs(seed) % 3;
        // Low lettuce, vining cucumber and upright tomato share a low-poly leaf layout.
        float height = type == 0 ? .32f + .15f * scale : scale * (type == 1 ? .91f : .98f);
        float phase = seed * 2.39996f;
        if (type != 0)
            Beam(Interior, root, root + Vector3.up * height, .034f);
        int tiers = height > 1.8f ? 11 : 7;
        for (int leaf = 0; leaf < tiers; leaf++)
        {
            float angle = phase + leaf * 2.03f;
            Vector3 forward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 across = Vector3.Cross(Vector3.up, forward);
            float growth = leaf / (float)tiers;
            float length = (.33f + Mathf.Min(scale, 3.7f) * .126f) * (1f - growth * .45f);
            float width = length * (type == 1 ? .44f : type == 2 ? .27f : .52f);
            Vector3 start = root + Vector3.up * height * (.12f + growth * .82f);
            Vector3 center = start + forward * length * .42f + Vector3.up * length * .23f;
            Vector3 shoulder = center + Vector3.up * length * .10f;
            Vector3 tip = start + forward * length + Vector3.up * length * (type == 1 ? .39f : .19f);
            int material = (leaf + type) % 3 == 0 ? Interior : Seat;
            Vector3 left = center + across * width, right = center - across * width;
            Leaf(start, left, shoulder, right, tip, material);
            if (type != 0 && leaf % 3 == 1)
            {
                Vector3 fruit = center - Vector3.up * .105f;
                float halfHeight = type == 1 ? .184f : .086f;
                Vector3 top = fruit + Vector3.up * halfHeight, bottom = fruit - Vector3.up * halfHeight;
                int color = type == 1 ? Interior : Trim;
                for (int sector = 0; sector < 4; sector++)
                {
                    Vector3 p = fruit + Quaternion.Euler(0f, 90f * sector, 0f) * Vector3.right * .071f;
                    Vector3 q = fruit + Quaternion.Euler(0f, 90f * (sector + 1), 0f) * Vector3.right * .071f;
                    Triangle(color, top, p, q);
                    Triangle(color, bottom, q, p);
                }
            }
        }
    }

    void Leaf(Vector3 stem, Vector3 left, Vector3 rib, Vector3 right, Vector3 tip, int material)
    {
        int first = vertices.Count;
        var triangles = faces[material];
        for (int side = 0; side < 2; side++)
        {
            int offset = first + side * 5;
            vertices.Add(stem);
            vertices.Add(left);
            vertices.Add(rib);
            vertices.Add(right);
            vertices.Add(tip);
            Face(0, 1, 2);
            Face(2, 1, 4);
            Face(0, 2, 3);
            Face(2, 4, 3);

            void Face(int a, int b, int c)
            {
                triangles.Add(offset + a);
                triangles.Add(offset + (side == 0 ? b : c));
                triangles.Add(offset + (side == 0 ? c : b));
            }
        }
    }

    void Foundation(float x, float z, float width, float depth)
    {
        Box(Dark, new Vector3(x, -1.08f, z), new Vector3(width, 3.12f, depth));
        Box(Frame, new Vector3(x, .54f, z), new Vector3(width + .06f, .16f, depth + .06f));
    }

    void Pump(Vector3 center)
    {
        Axial(Frame, center, Vector3.right, 12, (-.53f, .39f), (.45f, .39f), (.58f, .25f));
        Box(Dark, center + Vector3.down * .38f, new Vector3(1.48f, .18f, .97f));
        Box(Beacon, center + new Vector3(.17f, .26f, .27f), new Vector3(.27f, .12f, .12f));
    }

    void Pipe(Vector3 from, Vector3 to, float radius)
    {
        Vector3 direction = to - from;
        Axial(Frame, from, direction.normalized, 10, (0f, radius * 1.28f), (.13f, radius),
            (direction.magnitude - .13f, radius), (direction.magnitude, radius * 1.28f));
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

    void ReleaseBuildBuffers()
    {
        vertices.TrimExcess();
        glassVertices.TrimExcess();
        foreach (List<int> face in faces) face.TrimExcess();
    }

    void InteriorLight(Vector3 position, Color color, float range, float intensity)
    {
        assets.Lamps.Add((position,color,range,intensity));
        CreateInteriorLight(position,color,range,intensity);
    }

    void CreateInteriorLight(Vector3 position, Color color, float range, float intensity)
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
        interiorLights.Add(light);
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
        var material = new Material(urpLitTemplate) { name = name, enableInstancing = true };
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
        foreach (Mesh part in machineryMeshes) Destroy(part);
        if (materials != null)
        {
            Destroy(materials[Window]);
            Destroy(materials[Beacon]);
            Destroy(materials[Interior]);
        }
        if (assets==null || --assets.Users>0) return;
        common.Remove((kind,urpLitTemplate));
        Destroy(assets.GlassMesh);
        Destroy(assets.ShellMesh);
        if(assets.Palette!=null)
            foreach(Material material in assets.Palette) Destroy(material);
    }
}
