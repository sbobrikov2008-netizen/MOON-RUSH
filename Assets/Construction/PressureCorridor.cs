using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class PressureCorridor : MonoBehaviour
{
    internal const float HalfWidth = 1.4f;
    internal const float Floor = -1.15f;
    internal const float Roof = 1.48f;
    [SerializeField] Material metal;
    [SerializeField] Material glass;

    readonly List<Vector3> vertices = new List<Vector3>();
    const int Hull = 0, Glass = 1, Lamp = 2, Collision = 3, Deck = 4, Markings = 5, Cable = 6;
    readonly List<int>[] triangles = { new List<int>(), new List<int>(), new List<int>(), new List<int>(),
        new List<int>(), new List<int>(), new List<int>() };
    readonly List<Light> fixtures = new List<Light>();
    static readonly Color WarmEmission = new Color(.59f, .53f, .44f);
    Material deck, markings, conduitMaterial;
    bool powered = true;
    float nextFixture;
    Transform geometry;
    Mesh mesh;
    Mesh collisionMesh;
    Material hull;
    Material lamp;
    internal bool VisualOnly { get; set; }
    internal Material PreviewMaterial { get; set; }
    internal MeshRenderer ShellRenderer { get; private set; }
    public BuildingInstance First { get; private set; }
    public BuildingInstance Second { get; private set; }
    public IReadOnlyList<Vector3> Route { get; private set; }
    internal Transform StartPort { get; private set; }
    internal Transform EndPort { get; private set; }

    void Awake()
    {
        hull = new Material(metal) { name = "Satin pressure hull" };
        hull.SetColor("_BaseColor", new Color(.33f, .38f, .42f));
        hull.SetFloat("_Smoothness", .22f);
        hull.SetFloat("_Metallic", .25f);
        hull.DisableKeyword("_EMISSION");
        hull.SetColor("_EmissionColor", Color.black);
        lamp = new Material(hull) { name = "Low-output corridor strips" };
        lamp.EnableKeyword("_EMISSION");
        lamp.SetColor("_BaseColor", new Color(.77f, .74f, .67f));
        lamp.SetColor("_EmissionColor", WarmEmission);
        deck = new Material(hull) { name = "Embossed ceramic decking" };
        deck.SetColor("_BaseColor", new Color(.43f, .45f, .47f));
        deck.SetFloat("_Metallic", .48f);
        markings = new Material(hull) { name = "Safety stencils and brackets" };
        markings.SetColor("_BaseColor", new Color(.76f, .43f, .14f));
        conduitMaterial = new Material(hull) { name = "Insulated pressure services" };
        conduitMaterial.SetColor("_BaseColor", new Color(.16f, .20f, .23f));
    }

    internal void SetPowered(bool enabled)
    {
        powered = enabled;
        lamp.SetColor("_EmissionColor", enabled ? WarmEmission : Color.black);
        lamp.SetColor("_BaseColor", enabled ? new Color(.77f, .74f, .67f) : new Color(.18f, .15f, .12f));
        foreach (Light fixture in fixtures) fixture.enabled = enabled;
    }

    internal void BuildCatalogSample()
    {
        VisualOnly = true;
        BeginGeometry();
        AddSpan(transform.TransformPoint(new Vector3(-9f, 1.8f, 0f)),
            transform.TransformPoint(new Vector3(9f, 1.8f, 0f)), null, true, true);
        FinishGeometry();
    }

    internal void Connect(BuildingInstance first, BuildingInstance second, Vector3[] route,
        Transform start, Transform end)
    {
        First = first;
        Second = second;
        Route = route;
        StartPort = start;
        EndPort = end;
    }

    internal void BeginGeometry()
    {
        if (geometry != null)
        {
            geometry.gameObject.SetActive(false);
            Destroy(geometry.gameObject);
            Destroy(mesh);
            Destroy(collisionMesh);
            collisionMesh = null;
        }
        geometry = new GameObject("Pressure hull and collision").transform;
        geometry.SetParent(transform, false);
        geometry.gameObject.layer = gameObject.layer;
        fixtures.Clear();
        nextFixture = 1.05f;
        nextRib = 1.62f;
        nextSupport = 1.1f;
        vertices.Clear();
        foreach (List<int> indices in triangles) indices.Clear();
    }

    const float Shoulder = .08f;
    const int ArcSegments = 12;

    internal static List<(Vector3 point, Vector3 axis)> SmoothCenterline(IReadOnlyList<Vector3> route, out bool fits)
    {
        fits = true;
        var sections = new List<(Vector3, Vector3)> { (route[0], Horizontal(route[1] - route[0])) };
        for (int i = 1; i < route.Count - 1; i++)
        {
            Vector3 a = route[i - 1], b = route[i], c = route[i + 1];
            Vector3 incoming = Horizontal(b - a), outgoing = Horizontal(c - b);
            float dot = Mathf.Clamp(Vector3.Dot(incoming, outgoing), -1f, 1f);
            if (dot > .9995f)
            {
                sections.Add((b, outgoing));
                continue;
            }
            bool previousBends = i > 1 && Vector3.Dot(Horizontal(a - route[i - 2]), incoming) < .9995f;
            bool nextBends = i + 2 < route.Count && Vector3.Dot(outgoing, Horizontal(route[i + 2] - c)) < .9995f;
            float runA = new Vector2(b.x - a.x, b.z - a.z).magnitude;
            float runB = new Vector2(c.x - b.x, c.z - b.z).magnitude;
            float angle = Mathf.Acos(dot);
            float tangent = Mathf.Sin(angle * .5f) / Mathf.Cos(angle * .5f);
            float trim = Mathf.Min(2.30f, runA * (previousBends ? .47f : .90f), runB * (nextBends ? .47f : .90f));
            float radius = trim / tangent;
            if (radius < HalfWidth + .13f) fits = false;
            Vector3 start = Vector3.Lerp(b, a, trim / runA);
            Vector3 finish = Vector3.Lerp(b, c, trim / runB);
            Vector3 towardCenter = (outgoing - incoming * dot).normalized;
            Vector3 center = start + towardCenter * radius;
            const int steps = 12;
            for (int step = 0; step <= steps; step++)
            {
                float t = step / (float)steps, theta = angle * t;
                Vector3 point = center + radius * (incoming * Mathf.Sin(theta) - towardCenter * Mathf.Cos(theta));
                point.y = Mathf.Lerp(start.y, finish.y, t);
                sections.Add((point, Horizontal(incoming * Mathf.Cos(theta) + towardCenter * Mathf.Sin(theta))));
            }
        }
        sections.Add((route[route.Count - 1], Horizontal(route[route.Count - 1] - route[route.Count - 2])));
        return sections;
    }

    internal void BuildRoute(IReadOnlyList<Vector3> route,Terrain terrain)
    {
        var sections=SmoothCenterline(route,out _);
        for(int i=1;i<sections.Count;i++)
        {
            var a=sections[i-1]; var b=sections[i];
            Sweep(a.point,b.point,a.axis,b.axis,terrain);
        }
        Dock(sections[0].point,sections[0].axis);
        Dock(sections[sections.Count-1].point,-sections[sections.Count-1].axis);
    }

    internal void AddSpan(Vector3 from,Vector3 to,Terrain terrain,bool firstCollar,bool lastCollar)
    {
        Vector3 axis=Horizontal(to-from);
        Sweep(from,to,axis,axis,terrain);
        if(firstCollar) Dock(from,axis);
        if(lastCollar) Dock(to,-axis);
    }

    float nextRib, nextSupport;

    void Sweep(Vector3 from,Vector3 to,Vector3 tangentA,Vector3 tangentB,Terrain terrain)
    {
        float length=Vector3.Distance(from,to);
        if(length<.005f || tangentA==Vector3.zero || tangentB==Vector3.zero) return;
        Vector3 rightA=Vector3.Cross(Vector3.up,tangentA), rightB=Vector3.Cross(Vector3.up,tangentB);
        Vector3 floor=Vector3.up*Floor;
        Panel(from+floor-rightA*HalfWidth,from+floor+rightA*HalfWidth,
            to+floor+rightB*HalfWidth,to+floor-rightB*HalfWidth,Vector3.up,Deck);

        for(int side=-1;side<=1;side+=2)
        {
            Vector3 a=rightA*side,b=rightB*side;
            Sidewall(from+a*HalfWidth,to+b*HalfWidth,(a+b).normalized);
            ServiceLine(1.22f,Floor+.94f,.063f,.063f,Hull);
            ServiceLine(1.28f,Floor+.16f,.115f,.12f,Cable);
            ServiceLine(1.31f,Floor+.33f,.061f,.061f,Hull);
            ServiceLine(1.23f,Floor+.023f,.029f,.031f,Lamp);

            void ServiceLine(float offset,float height,float width,float depth,int material)
            {
                Vector3 up=Vector3.up*height;
                Rail(from+a*offset+up,to+b*offset+up,width,depth,material);
            }
        }
        for(int arc=0;arc<ArcSegments;arc++)
        {
            Vector3 a=Arch(arc,rightA), b=Arch(arc+1,rightA);
            Vector3 c=Arch(arc+1,rightB), d=Arch(arc,rightB);
            Vector3 normal=(a+b+c+d)*.25f-Vector3.up*Shoulder;
            Panel(from+a,from+b,to+c,to+d,normal,Glass);
        }

        while(nextRib<length)
        {
            Position(nextRib,out Vector3 point,out Vector3 axis);
            Rib(point,axis,.09f);
            Vector3 right=Vector3.Cross(Vector3.up,axis);
            for(int side=-1;side<=1;side+=2)
            {
                Box(point+right*(side*1.28f)+Vector3.up*(Floor+.18f),
                    new Vector3(.16f,.23f,.12f),axis,Markings);
                Box(point+right*(side*1.27f)+Vector3.up*(Floor+.91f),
                    new Vector3(.16f,.13f,.11f),axis,Hull);
            }
            nextRib+=3.25f;
        }
        while(nextFixture<length)
        {
            Position(nextFixture,out Vector3 point,out Vector3 axis);
            Vector3 hood=point+Vector3.up*(Roof-.105f);
            Box(hood,new Vector3(.57f,.11f,1.32f),axis,Hull);
            Box(hood-Vector3.up*.071f,new Vector3(.44f,.032f,1.18f),axis,Lamp);
            if(!VisualOnly) AddFixture(point, axis);
            nextFixture+=4.8f;
        }
        while(nextSupport<length)
        {
            if(terrain!=null)
            {
                Position(nextSupport,out Vector3 point,out _);
                Support(point,terrain,.72f);
            }
            nextSupport+=3.9f;
        }
        nextRib-=length; nextFixture-=length; nextSupport-=length;

        void Position(float distance,out Vector3 point,out Vector3 axis)
        {
            float t=distance/length;
            point=Vector3.Lerp(from,to,t);
            axis=Horizontal(Vector3.Lerp(tangentA,tangentB,t));
        }
    }

    void AddFixture(Vector3 at, Vector3 axis)
    {
        var fixture = new GameObject("Warm deck light", typeof(Light));
        fixture.transform.SetParent(geometry, false);
        fixture.transform.position = at + Vector3.up * (Roof - .23f);
        var light = fixture.GetComponent<Light>();
        fixture.transform.rotation = Quaternion.LookRotation(Vector3.down, axis);
        light.type = LightType.Spot;
        light.spotAngle = 119f;
        light.innerSpotAngle = 72f;
        light.color = new Color(1f, .94f, .86f);
        light.range = 5.4f;
        light.intensity = 5.6f;
        light.shadows = LightShadows.None;
        light.enabled = powered;
        fixtures.Add(light);
    }

    static Vector3 Arch(int section, Vector3 right)
    {
        float angle = section * Mathf.PI / ArcSegments;
        return right * (HalfWidth * Mathf.Cos(angle)) +
            Vector3.up * (Shoulder + (Roof - Shoulder) * Mathf.Sin(angle));
    }

    void Panel(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, int material)
    {
        Face(a, b, c, d, normal, material, true);
        if (!VisualOnly) Face(a, b, c, d, normal, Collision, true);
    }

    void Sidewall(Vector3 a, Vector3 b, Vector3 outward)
    {
        Panel(a + Vector3.up * Floor, b + Vector3.up * Floor, b + Vector3.up * Shoulder,
            a + Vector3.up * Shoulder, outward, 1);
        Rail(a + Vector3.up * Floor, b + Vector3.up * Floor, .085f, .14f, 0);
    }

    void Support(Vector3 point, Terrain terrain, float width)
    {
        float ground = terrain.transform.position.y + terrain.SampleHeight(point);
        float rise = point.y + Floor - ground;
        if (rise > .08f)
            Box(new Vector3(point.x, ground + rise * .5f, point.z),
                new Vector3(width, rise + .13f, width), Vector3.forward, 0);
    }

    void Rib(Vector3 point, Vector3 axis, float thickness)
    {
        Vector3 right = Vector3.Cross(Vector3.up, axis);
        for (int section = 0; section < ArcSegments; section++)
        {
            Vector3 a = Arch(section, right), b = Arch(section + 1, right);
            RibPiece(point + a, point + b);
        }
        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 edge = right * side * HalfWidth;
            RibPiece(point + edge + Vector3.up * Floor, point + edge + Vector3.up * Shoulder);
        }

        void RibPiece(Vector3 a, Vector3 b)
        {
            Vector3 width = Vector3.Cross(axis, (b - a).normalized) * .046f;
            Vector3 depth = axis * thickness * .5f;
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Face(a + width * sign - depth, b + width * sign - depth, b + width * sign + depth,
                    a + width * sign + depth, width * sign, 0);
                Face(a - width + depth * sign, b - width + depth * sign, b + width + depth * sign,
                    a + width + depth * sign, depth * sign, 0);
            }
        }
    }

    void Dock(Vector3 point, Vector3 axis)
    {
        Rib(point + axis * .08f, axis, .16f);
        Rib(point + axis * .26f, axis, .085f);
        Vector3 right = Vector3.Cross(Vector3.up, axis);
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 location = point + right * (side * 1.27f) + axis * .26f + Vector3.up * (Floor + .27f);
            Box(location, new Vector3(.14f, .32f, .19f), axis, 0);
            Box(location + Vector3.up * .17f, new Vector3(.065f, .041f, .09f), axis, 2);
        }
        for (int section = 0; section < ArcSegments; section++)
        {
            Vector3 a = Arch(section, right), b = Arch(section + 1, right);
            if ((a - b).sqrMagnitude < .0001f) continue;
            Face(point + a, point + b, point + new Vector3(b.x, Roof + .045f, b.z),
                point + new Vector3(a.x, Roof + .045f, a.z), axis, 0, true);
        }
    }

    void Rail(Vector3 a, Vector3 b, float width, float height, int material)
    {
        Vector3 delta = b - a;
        if (delta.sqrMagnitude < .0001f) return;
        Box((a + b) * .5f, new Vector3(width, height, delta.magnitude), delta.normalized, material);
    }

    void Box(Vector3 center, Vector3 size, Vector3 axis, int material)
    {
        Quaternion rotation = Quaternion.LookRotation(axis);
        Vector3 x = rotation * Vector3.right * size.x * .5f;
        Vector3 y = rotation * Vector3.up * size.y * .5f;
        Vector3 z = rotation * Vector3.forward * size.z * .5f;
        Side(x, y, z); Side(y, z, x); Side(z, x, y);
        Side(-x, y, z); Side(-y, z, x); Side(-z, x, y);

        void Side(Vector3 normal, Vector3 u, Vector3 v)
        {
            Vector3 origin = center + normal;
            Face(origin - u - v, origin + u - v, origin + u + v, origin - u + v, normal, material);
        }
    }

    void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, int material, bool twoSided = false)
    {
        // Each side owns its vertices. Sharing them between reversed indices cancels the normals.
        bool reverse = Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f;
        AddTriangle(a, reverse ? c : b, reverse ? b : c);
        if (c != d) AddTriangle(a, reverse ? d : c, reverse ? c : d);
        if (!twoSided) return;
        AddTriangle(a, reverse ? b : c, reverse ? c : b);
        if (c != d) AddTriangle(a, reverse ? c : d, reverse ? d : c);
        void AddTriangle(Vector3 x, Vector3 y, Vector3 z)
        {
            var indices = triangles[material];
            indices.Add(vertices.Count);
            indices.Add(vertices.Count + 1);
            indices.Add(vertices.Count + 2);
            vertices.Add(transform.InverseTransformPoint(x));
            vertices.Add(transform.InverseTransformPoint(y));
            vertices.Add(transform.InverseTransformPoint(z));
        }
    }

    internal void FinishGeometry()
    {
        mesh = new Mesh { name = "Pressure shell", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        var uv = new List<Vector2>(vertices.Count);
        foreach (Vector3 position in vertices) uv.Add(new Vector2(position.x * .71f, position.z * .71f));
        mesh.SetUVs(0, uv);
        int[] visual = { Hull, Glass, Lamp, Deck, Markings, Cable };
        mesh.subMeshCount = visual.Length;
        for (int i = 0; i < visual.Length; i++) mesh.SetTriangles(triangles[visual[i]], i);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var shell = new GameObject("Metal skin and glazing", typeof(MeshFilter), typeof(MeshRenderer));
        shell.transform.SetParent(geometry, false);
        shell.layer = gameObject.layer;
        shell.GetComponent<MeshFilter>().sharedMesh = mesh;
        ShellRenderer = shell.GetComponent<MeshRenderer>();
        ShellRenderer.sharedMaterials = PreviewMaterial == null ? new[] { hull, glass, lamp, deck, markings, conduitMaterial } :
            new[] { PreviewMaterial, PreviewMaterial, PreviewMaterial, PreviewMaterial, PreviewMaterial, PreviewMaterial };
        if (PreviewMaterial != null) ShellRenderer.shadowCastingMode = ShadowCastingMode.Off;
        if (!VisualOnly && triangles[Collision].Count > 0)
        {
            // The collision surface follows the curved shell and leaves the mouths open.
            collisionMesh = new Mesh { name = "Pressure shell collision", indexFormat = IndexFormat.UInt32 };
            collisionMesh.SetVertices(vertices);
            collisionMesh.SetTriangles(triangles[Collision], 0);
            collisionMesh.RecalculateBounds();
            shell.AddComponent<MeshCollider>().sharedMesh = collisionMesh;
        }
    }

    internal static Vector3 Horizontal(Vector3 vector)
    {
        vector.y = 0f;
        return vector.normalized;
    }

    public bool Occupies(Vector3 point, float clearance)
    {
        if (Route == null) return false;
        for (int i = 1; i < Route.Count; i++)
            if (DistanceToSegment(point, Route[i - 1], Route[i]) < clearance) return true;
        return false;
    }

    internal static float Projection(Vector3 point, Vector3 a, Vector3 b)
    {
        point.y = a.y = b.y = 0f;
        Vector3 delta = b - a;
        return Mathf.Clamp01(Vector3.Dot(point - a, delta) / Mathf.Max(delta.sqrMagnitude, .0001f));
    }

    public static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 projected = Vector3.Lerp(a, b, Projection(point, a, b));
        projected.y = point.y;
        return Vector3.Distance(point, projected);
    }

    void OnDestroy()
    {
        Destroy(mesh);
        Destroy(collisionMesh);
        Destroy(hull);
        Destroy(lamp);
        Destroy(deck);
        Destroy(markings);
        Destroy(conduitMaterial);
    }
}
