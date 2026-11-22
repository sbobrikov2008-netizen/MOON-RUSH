using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider))]
public sealed class LunarLandingPad : MonoBehaviour
{
    [SerializeField] Material urpLitTemplate;

    const float RampStart = 18f;
    const float RampEnd = 31f;
    const float RampHalfWidth = 4.2f;
    const float RampDeckHeight = .67f;
    const int RampRows = 20;
    const int RampColumns = 8;

    public static Rect RampFootprint => Rect.MinMaxRect(-4.55f, 16.4f, 4.55f, 31.1f);
    public Vector2 RampToeHeights => new Vector2(rampPoints[RampRows, 0].y, rampPoints[RampRows, RampColumns].y);

    Mesh mesh;
    Mesh rampMesh;
    MeshCollider rampCollider;
    Vector3 rampOrigin;
    Terrain previousTerrain;
    bool rampFits;
    readonly Vector3[,] rampPoints = new Vector3[RampRows + 1, RampColumns + 1];
    Material[] materials;
    readonly List<Vector3> vertices = new List<Vector3>();
    readonly List<int>[] faces = {
        new List<int>(), new List<int>(), new List<int>(), new List<int>(),
        new List<int>(), new List<int>(), new List<int>(), new List<int>()
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
            if (side == 0) continue; // Keep the rover access clear.
            Box(5, Axis(19.1f, .64f), new Vector3(2.6f, .22f, .83f), angle);
            Box(4, Axis(19.1f, .782f), new Vector3(1.28f, .064f, .24f), angle);
            if (side % 2 == 0)
            {
                Box(1, Axis(18.64f, .71f, 3.85f), new Vector3(1.58f, .47f, 1.07f), angle);
                Box(5, Axis(18.64f, .95f, 3.85f), new Vector3(1.37f, .05f, .75f), angle);
                Box(3, Axis(18.64f, .983f, 3.85f), new Vector3(.97f, .035f, .12f), angle);
            }
        }

        BuildServiceEquipment();
        mesh = new Mesh { name = "Landing pad, service equipment and antenna" };
        Upload(mesh);
        materials = new[]
        {
            Metal("Sintered landing deck", new Color(.25f, .27f, .29f), .21f, .27f),
            Metal("Brushed frame", new Color(.36f, .43f, .48f), .68f, .44f),
            Metal("Ceramic markings", new Color(.86f, .89f, .86f), .05f, .39f),
            Metal("Safety amber", new Color(.88f, .46f, .10f), .22f, .36f),
            Metal("Guidance light", new Color(.19f, .8f, .91f), 0f, .49f),
            Metal("Heat shield and foundation", new Color(.12f, .145f, .16f), .28f, .29f),
            Metal("Red obstruction beacon", new Color(.7f, .06f, .035f), .02f, .37f),
            Metal("Shielded inspection glass", new Color(.08f, .27f, .35f), .42f, .32f)
        };
        materials[4].EnableKeyword("_EMISSION");
        materials[4].SetColor("_EmissionColor", new Color(.17f, .86f, 1f) * 1.9f);
        materials[6].EnableKeyword("_EMISSION");
        materials[6].SetColor("_EmissionColor", new Color(1f, .095f, .035f) * 2.1f);
        GetComponent<MeshFilter>().sharedMesh = mesh;
        GetComponent<MeshRenderer>().sharedMaterials = materials;
        var collider = GetComponent<BoxCollider>();
        collider.center = new Vector3(0f, -1.43f, 0f);
        collider.size = new Vector3(36.6f, 3.9f, 36.6f);
        var ramp = new GameObject("Ribbed rover ramp", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        ramp.transform.SetParent(transform, false);
        rampMesh = new Mesh { name = "Terrain-fitted rover ramp" };
        rampMesh.MarkDynamic();
        ramp.GetComponent<MeshFilter>().sharedMesh = rampMesh;
        ramp.GetComponent<MeshRenderer>().sharedMaterials = materials;
        rampCollider = ramp.GetComponent<MeshCollider>();
        FitRamp(null, transform.position, false);

        var mast = new GameObject("Antenna clearance", typeof(BoxCollider));
        mast.transform.SetParent(transform, false);
        var antennaCollider = mast.GetComponent<BoxCollider>();
        antennaCollider.center = new Vector3(-16.2f, 5.75f, -1.9f);
        antennaCollider.size = new Vector3(3f, 10.2f, 3f);
        var cabin = new GameObject("Control module clearance", typeof(BoxCollider));
        cabin.transform.SetParent(transform, false);
        var moduleCollider = cabin.GetComponent<BoxCollider>();
        moduleCollider.center = new Vector3(10.8f, 1.7f, -12.65f);
        moduleCollider.size = new Vector3(5.8f, 2.4f, 4f);
    }

    void BuildServiceEquipment()
    {
        Vector3 mast = new Vector3(-16.2f, 0f, -1.9f);
        Box(5, mast + Vector3.up * .64f, new Vector3(3.05f, .23f, 3.05f), 0f);
        Vector3 Leg(int leg, int level)
        {
            float angle = leg * Mathf.PI * 2f / 3f + .34f;
            float r = Mathf.Lerp(1.26f, .33f, level / 5f);
            return mast + new Vector3(r * Mathf.Cos(angle), .75f + level * 1.82f, r * Mathf.Sin(angle));
        }
        for (int level = 0; level < 5; level++)
        for (int leg = 0; leg < 3; leg++)
        {
            int next = (leg + 1) % 3;
            Beam(1, Leg(leg, level), Leg(leg, level + 1), .16f);
            Beam(1, Leg(leg, level), Leg(next, level), .10f);
            Beam(1, Leg(leg, level), Leg(next, level + 1), .095f);
            Beam(5, Leg(next, level), Leg(leg, level + 1), .075f);
        }
        Beam(1, mast + Vector3.up * 8.25f, mast + Vector3.up * 10.82f, .13f);
        Box(6, mast + Vector3.up * 10.73f, new Vector3(.34f, .29f, .34f), 45f);
        for (int strip = 0; strip < 3; strip++)
        {
            float height = 7.05f + strip * .63f;
            float span = 1.65f - strip * .27f;
            Beam(1, mast + new Vector3(-span, height, 0f), mast + new Vector3(span, height, 0f), .095f);
            Box(2, mast + new Vector3(-span * .86f, height, 0f), new Vector3(.16f, .62f, .33f), 0f);
            Box(2, mast + new Vector3(span * .86f, height, 0f), new Vector3(.16f, .62f, .33f), 0f);
        }

        Vector3 cabin = new Vector3(10.8f, 0f, -12.65f);
        Box(5, cabin + Vector3.up * .67f, new Vector3(5.7f, .34f, 3.8f), 0f);
        Box(1, cabin + Vector3.up * 1.66f, new Vector3(5.35f, 1.94f, 3.52f), 0f);
        Box(5, cabin + Vector3.up * 2.75f, new Vector3(5.63f, .26f, 3.76f), 0f);
        for (int pane = -1; pane <= 1; pane++)
            Box(7, cabin + new Vector3(pane * 1.42f, 2.05f, 1.78f), new Vector3(1.28f, .68f, .13f), 0f);
        Box(5, cabin + new Vector3(-2.35f, 1.35f, 1.79f), new Vector3(.63f, 1.22f, .09f), 0f);
        for (int slot = 0; slot < 6; slot++)
            Box(5, cabin + new Vector3(2.7f, 1.28f + slot * .18f, -.92f), new Vector3(.075f, .052f, 1.34f), 0f);

        Vector3 dish = new Vector3(-7.7f, 3.63f, -14.8f);
        Beam(1, dish + Vector3.down * 3.05f, dish, .33f);
        Box(5, dish + Vector3.down * 2.97f, new Vector3(2.1f, .28f, 2f), 16f);
        Quaternion facing = Quaternion.Euler(-37f, -26f, 0f);
        const int dishSides = 28;
        const int dishRings = 5;
        Vector3 Bowl(float radius, int side)
        {
            float angle = side * Mathf.PI * 2f / dishSides;
            return dish + facing * new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius,
                .22f * radius * radius);
        }
        for (int ring = 0; ring < dishRings; ring++)
        for (int side = 0; side < dishSides; side++)
        {
            float inner = ring * 1.59f / dishRings;
            float outer = (ring + 1) * 1.59f / dishRings;
            int skin = ring >= dishRings - 1 ? 1 : 2;
            Vector3 a = Bowl(inner, side), b = Bowl(outer, side);
            Vector3 c = Bowl(outer, side + 1), d = Bowl(inner, side + 1);
            if (ring == 0) Triangle(skin, a, b, c);
            else Quad(skin, a, b, c, d);
            Vector3 thickness = facing * Vector3.back * .055f;
            if (ring == 0) Triangle(5, a + thickness, c + thickness, b + thickness);
            else Quad(5, d + thickness, c + thickness, b + thickness, a + thickness);
        }
        Vector3 feed = dish + facing * Vector3.forward * 1.42f;
        for (int spoke = 0; spoke < 3; spoke++) Beam(1, Bowl(1.42f, spoke * 9), feed, .074f);
        Box(1, feed, new Vector3(.23f, .23f, .31f), -26f);

        for (int i = 0; i < 4; i++)
        {
            Vector3 crate = new Vector3(14.1f + i * .44f, .81f, -.8f - i * 2.06f);
            Box(5, crate, new Vector3(1.85f, .64f, 1.3f), 0f);
            Box(1, crate + Vector3.up * .34f, new Vector3(1.9f, .09f, 1.35f), 0f);
            Box(3, crate + new Vector3(.24f, .395f, .08f), new Vector3(.4f, .035f, .32f), 0f);
        }
        for (int cable = 0; cable < 3; cable++)
        {
            float x = 13.5f + cable * .25f;
            Beam(5, new Vector3(x, .57f, -11.1f), new Vector3(x, .57f, -8.25f), .099f);
            Beam(5, new Vector3(x, .57f, -8.25f), new Vector3(x + 1.3f, .57f, -7.2f), .099f);
            Beam(5, new Vector3(x + 1.3f, .57f, -7.2f), new Vector3(x + 1.3f, .57f, -2.5f), .099f);
        }
        for (int side = -1; side <= 1; side += 2)
        {
            Box(5, new Vector3(side * 5.0f, .72f, 17.4f), new Vector3(.85f, .34f, .87f), 0f);
            Box(4, new Vector3(side * 5.0f, .92f, 17.4f), new Vector3(.49f, .08f, .51f), 0f);
        }
        Quad(1, new Vector3(-RampHalfWidth, .52f, 16.5f),
            new Vector3(-RampHalfWidth, RampDeckHeight, RampStart),
            new Vector3(RampHalfWidth, RampDeckHeight, RampStart),
            new Vector3(RampHalfWidth, .52f, 16.5f));
    }

    public bool FitRamp(Terrain terrain, Vector3 origin, bool updateCollider)
    {
        if (rampMesh == null) return false;
        if (terrain != null && previousTerrain == terrain && (origin - rampOrigin).sqrMagnitude < .0004f)
            return rampFits;
        previousTerrain = terrain;
        rampOrigin = origin;
        bool fits = true;
        float run = RampEnd - RampStart;
        float step = run / RampRows;
        float span = RampHalfWidth * 2f / RampColumns;
        const float maximumGrade = .364f;
        for (int column = 0; column <= RampColumns; column++)
        {
            float x = -RampHalfWidth + column * span;
            float end = terrain != null ? Ground(x, RampEnd) + .025f : -1.56f;
            if (end > RampDeckHeight + .12f) fits = false;
            for (int row = 0; row <= RampRows; row++)
            {
                float t = row / (float)RampRows;
                float z = RampStart + t * run;
                float ease = t - .26f * Mathf.Sin(t * Mathf.PI * 2f) / (Mathf.PI * 2f);
                float design = Mathf.Lerp(RampDeckHeight, end, ease);
                float ground = terrain != null ? Ground(x, z) + .025f : -2f;
                if (row < RampRows && ground - design > .23f) fits = false;
                float y = row == 0 ? RampDeckHeight : Mathf.Max(design, ground);
                rampPoints[row, column] = new Vector3(x, y, z);
                if (row > 0 && Mathf.Abs(y - rampPoints[row - 1, column].y) > maximumGrade * step) fits = false;
                if (column > 0 && Mathf.Abs(y - rampPoints[row, column - 1].y) > .21f * span) fits = false;
            }
        }

        for (int row = 0; row < RampRows; row++)
        {
            for (int column = 0; column < RampColumns; column++)
                Quad(column % 2 == 0 ? 0 : 1, rampPoints[row, column], rampPoints[row + 1, column],
                    rampPoints[row + 1, column + 1], rampPoints[row, column + 1]);

            for (int side = 0; side <= RampColumns; side += RampColumns)
            {
                Vector3 a = rampPoints[row, side], b = rampPoints[row + 1, side];
                Vector3 drop = Vector3.down * .18f;
                if (side == 0) Quad(5, a, a + drop, b + drop, b);
                else Quad(5, b, b + drop, a + drop, a);
                Beam(3, a + Vector3.up * .045f, b + Vector3.up * .045f, .13f);
                if (row % 4 == 1)
                {
                    Box(5, a + Vector3.up * .095f, new Vector3(.34f, .19f, .31f), 0f);
                    Box(4, a + Vector3.up * .204f, new Vector3(.25f, .034f, .22f), 0f);
                }
            }

            for (int column = 0; column < RampColumns; column++)
                Beam(1, rampPoints[row, column] + Vector3.up * .028f,
                    rampPoints[row, column + 1] + Vector3.up * .028f, .058f);
        }
        for (int column = 0; column < RampColumns; column++)
        {
            Vector3 a = rampPoints[RampRows, column], b = rampPoints[RampRows, column + 1];
            Quad(5, a, a + Vector3.down * .18f, b + Vector3.down * .18f, b);
        }
        Upload(rampMesh);
        if (updateCollider)
        {
            rampCollider.sharedMesh = null;
            rampCollider.sharedMesh = rampMesh;
        }
        rampFits = fits;
        return fits;

        float Ground(float x, float z) => terrain.transform.position.y - origin.y +
            terrain.SampleHeight(new Vector3(origin.x + x, origin.y, origin.z + z));
    }

    void Upload(Mesh target)
    {
        target.Clear();
        target.SetVertices(vertices);
        target.subMeshCount = faces.Length;
        for (int i = 0; i < faces.Length; i++)
        {
            target.SetTriangles(faces[i], i);
            faces[i].Clear();
        }
        target.RecalculateNormals();
        target.RecalculateBounds();
        vertices.Clear();
    }

    void Beam(int material, Vector3 a, Vector3 b, float thickness)
    {
        Vector3 delta = b - a;
        if (delta.sqrMagnitude < .000001f) return;
        Box(material, (a + b) * .5f, new Vector3(thickness, delta.magnitude, thickness),
            Quaternion.FromToRotation(Vector3.up, delta));
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

    void Box(int material, Vector3 center, Vector3 size, float yaw) =>
        Box(material, center, size, Quaternion.Euler(0f, yaw, 0f));

    void Box(int material, Vector3 center, Vector3 size, Quaternion rotation)
    {
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
        if (rampMesh != null) Destroy(rampMesh);
        if (mesh != null) Destroy(mesh);
        if (materials == null) return;
        foreach (Material surface in materials) Destroy(surface);
    }
}
