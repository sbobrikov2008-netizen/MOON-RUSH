using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class TerrainFoundation : MonoBehaviour
{
    const float SampleSpacing = 1.35f;
    const float GroundOverlap = .12f;

    // A rectangle describes the contact base, while circular bases use their actual polygon.
    internal readonly struct Footprint
    {
        public readonly Rect Area;
        public readonly int Sides;
        public readonly float Angle;

        public Footprint(float x, float z, float width, float depth, int sides = 4, float angle = 0f)
        {
            Area = new Rect(x - width * .5f, z - depth * .5f, width, depth);
            Sides = sides;
            Angle = angle;
        }

        public Vector2 Corner(int index)
        {
            if (Sides == 4)
            {
                int side = index % 4;
                return new Vector2(side < 2 ? Area.xMin : Area.xMax,
                    side == 1 || side == 2 ? Area.yMax : Area.yMin);
            }
            float angle = Angle - index * Mathf.PI * 2f / Sides;
            return Area.center + new Vector2(Mathf.Cos(angle) * Area.width, Mathf.Sin(angle) * Area.height) * .5f;
        }
    }

    static readonly Footprint[] Landing = { new Footprint(0, 0, 41.2f, 41.2f, 8) };
    static readonly Footprint[] Command =
    {
        new Footprint(0, 0, 22.45f, 22.45f, 8, Mathf.PI / 8f), new Footprint(0, 11.6f, 3.5f, 5f)
    };
    static readonly Footprint[] Living =
    {
        new Footprint(0, 0, 22.74f, 22.74f, 24, Mathf.PI / 24f), new Footprint(0, 12f, 3.56f, 4.45f)
    };
    static readonly Footprint[] Greenhouse =
    {
        new Footprint(0, -.4f, 13.9f, 20.8f), new Footprint(0, 11.38f, 3.5f, 3.4f)
    };
    static readonly Footprint[] Utility = { new Footprint(0, -1.25f, 18.2f, 17.5f) };
    static readonly Footprint[] Mining = { new Footprint(0, -1.25f, 18.2f, 17.5f), new Footprint(-4.55f, 8.6f, 7.4f, 5.7f) };
    static readonly Footprint[] Airlock = { new Footprint(0,0,9f,10f), new Footprint(0,6.10f,3.25f,2.25f) };
    static readonly Footprint[] Hangar = { new Footprint(0, 0, 32f, 22f) };
    static readonly Footprint[] Node = { new Footprint(0, 0, 9.64f, 9.64f, 8, Mathf.PI / 8f) };
    static readonly Footprint[] Solar = SolarFeet();

    Mesh mesh;

    static Footprint[] For(BuildingKind kind)
    {
        switch (kind)
        {
            case BuildingKind.LandingPad: return Landing;
            case BuildingKind.CommandPost: return Command;
            case BuildingKind.Habitat: return Living;
            case BuildingKind.Greenhouse: return Greenhouse;
            case BuildingKind.AirlockCapsule: return Airlock;
            case BuildingKind.CargoHangar: return Hangar;
            case BuildingKind.DockingNode: return Node;
            case BuildingKind.SolarStation: return Solar;
            case BuildingKind.MaterialsStation: return Mining;
            default: return Utility;
        }
    }

    static Footprint[] SolarFeet()
    {
        var pads = new List<Footprint> { new Footprint(0, 12.9f, 6.8f, 3.05f), new Footprint(0, -.45f, 1.85f, 22.3f) };
        for (int row = -1; row <= 1; row++)
        for (int bank = -1; bank <= 1; bank += 2)
        for (int foot = -1; foot <= 1; foot += 2)
            pads.Add(new Footprint(bank * 5.73f + foot * 2.89f, row * 8.17f - .62f + bank * .57f, 1.36f, 1.61f));
        return pads.ToArray();
    }

    internal static bool Evaluate(Terrain terrain, BuildingDefinition definition, Vector3 hit,
        Quaternion facing, Rect ramp, out Vector3 origin, out string reason)
    {
        TerrainData data = terrain.terrainData;
        Vector3 terrainOrigin = terrain.transform.position;
        Vector3 size = data.size;
        float high = float.NegativeInfinity, low = float.PositiveInfinity;
        int count = 0, steep = 0;
        bool outside = false, excessive = false;

        foreach (Footprint pad in For(definition.kind))
        for (int edge = 0; edge < pad.Sides; edge++)
        {
            Vector2 a = pad.Corner(edge), b = pad.Corner(edge + 1);
            int across = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / SampleSpacing));
            for (int column = 0; column <= across; column++)
            {
                Vector2 border = Vector2.Lerp(a, b, column / (float)across);
                int radial = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(pad.Area.center, border) / SampleSpacing));
                for (int row = 1; row <= radial; row++)
                    Sample(Vector2.Lerp(pad.Area.center, border, row / (float)radial), true);
            }
            if (edge == 0) Sample(pad.Area.center, true);
        }
        if (ramp.width > 0f)
        {
            int rows = Mathf.Max(1, Mathf.CeilToInt(ramp.height / SampleSpacing));
            for (int row = 0; row <= rows; row++)
            for (int column = 0; column <= 2; column++)
                Sample(new Vector2(Mathf.Lerp(ramp.xMin, ramp.xMax, column * .5f),
                    Mathf.Lerp(ramp.yMin, ramp.yMax, row / (float)rows)), false);
        }

        origin = hit;
        if (count > 0) origin.y = high + .04f;
        if (outside || count == 0) reason = "Основанию или спуску не хватает сплошного грунта";
        else if (excessive || steep * 5 > count) reason = "Слишком крутой склон под основанием";
        else if (high - low > definition.maximumRelief) reason = "Перепад высоты превышает возможности фундамента";
        else reason = string.Empty;
        return reason.Length == 0;

        void Sample(Vector2 local, bool bearing)
        {
            Vector3 point = hit + facing * new Vector3(local.x, 0f, local.y) - terrainOrigin;
            float u = point.x / size.x, v = point.z / size.z;
            if (u < 0f || u >= 1f || v < 0f || v >= 1f ||
                data.IsHole((int)(u * data.holesResolution), (int)(v * data.holesResolution)))
            {
                outside = true;
                return;
            }
            if (!bearing) return;
            float height = terrainOrigin.y + data.GetInterpolatedHeight(u, v);
            high = Mathf.Max(high, height);
            low = Mathf.Min(low, height);
            float grade = data.GetSteepness(u, v);
            count++;
            if (grade > definition.maximumSlope) steep++;
            if (grade > definition.maximumSlope + 8f) excessive = true;
        }
    }

    internal static TerrainFoundation Add(GameObject building, Material material)
    {
        var root = new GameObject("Adaptive foundation");
        root.transform.SetParent(building.transform, false);
        var foundation = root.AddComponent<TerrainFoundation>();
        foundation.mesh = new Mesh { name = "Terrain-fitted footing" };
        root.GetComponent<MeshFilter>().sharedMesh = foundation.mesh;
        var renderer = root.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return foundation;
    }

    internal void Fit(Terrain terrain, BuildingKind kind)
    {
        var points = new List<Vector3>();
        var indices = new List<int>();
        foreach (Footprint pad in For(kind))
        for (int edge = 0; edge < pad.Sides; edge++)
        {
            Vector2 a = pad.Corner(edge), b = pad.Corner(edge + 1);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / SampleSpacing));
            for (int step = 0; step < steps; step++)
            {
                Vector3 p = Top(Vector2.Lerp(a, b, step / (float)steps), pad.Area.center);
                Vector3 q = Top(Vector2.Lerp(a, b, (step + 1f) / steps), pad.Area.center);
                int first = points.Count;
                points.Add(p);
                points.Add(q);
                points.Add(Bottom(q));
                points.Add(Bottom(p));
                indices.Add(first);
                indices.Add(first + 2);
                indices.Add(first + 1);
                indices.Add(first);
                indices.Add(first + 3);
                indices.Add(first + 2);
            }
        }
        mesh.Clear();
        mesh.SetVertices(points);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        Vector3 Bottom(Vector3 point)
        {
            float ground = terrain.transform.position.y + terrain.SampleHeight(transform.TransformPoint(point));
            point.y = Mathf.Min(-.03f, ground - transform.position.y - GroundOverlap);
            return point;
        }

        static Vector3 Top(Vector2 point, Vector2 center)
        {
            Vector2 inset = Vector2.MoveTowards(point, center, .09f);
            return new Vector3(inset.x, .09f, inset.y);
        }
    }

    void OnDestroy() => Destroy(mesh);
}
