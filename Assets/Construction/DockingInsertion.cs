using System.Collections.Generic;
using UnityEngine;

internal sealed class DockingInsertion
{
    // Prefab ports lie on the cardinal faces of the octagon.
    internal const float PortOffset = 4.4533f;
    internal const float PortHeight = 1.72f;

    public PressureCorridor Original { get; }
    public Vector3 Origin { get; }
    public Quaternion Facing { get; }
    public Vector3[] Before { get; }
    public Vector3[] After { get; }

    DockingInsertion(PressureCorridor original, Vector3 origin, Quaternion facing, Vector3[] before, Vector3[] after)
    {
        Original = original;
        Origin = origin;
        Facing = facing;
        Before = before;
        After = after;
    }

    public static bool TryPlan(Terrain terrain, BuildingDefinition node, PressureCorridor corridor, Vector3 pointer,
        out DockingInsertion plan, out string reason)
    {
        plan = null;
        reason = "Для узла нужен длинный прямой участок коридора";
        var route = corridor.Route;
        var distances = new float[route.Count];
        float nearest = float.PositiveInfinity;
        float split = 0f;
        Vector3 axis = Vector3.zero, center = Vector3.zero;
        for (int i = 1; i < route.Count; i++)
        {
            Vector3 a = route[i - 1], b = route[i];
            float length = FlatLength(b - a);
            distances[i] = distances[i - 1] + length;
            float projection = PressureCorridor.Projection(pointer, a, b);
            Vector3 candidate = Vector3.Lerp(a, b, projection);
            float distance = FlatLength(candidate - pointer);
            if (length < .001f || distance >= nearest) continue;
            nearest = distance;
            split = distances[i - 1] + length * projection;
            axis = PressureCorridor.Horizontal(b - a);
            center = candidate;
        }

        float start = split - PortOffset, finish = split + PortOffset;
        const float minimumLeg = 2.7f;
        if (axis == Vector3.zero || start < minimumLeg || distances[route.Count - 1] - finish < minimumLeg)
            return false;

        // Leave space for the original bend collars outside each replacement mouth.
        for (int i = 1; i < route.Count - 1; i++)
        {
            if (distances[i] < start - 1.9f || distances[i] > finish + 1.9f) continue;
            if (Vector3.Dot(axis, PressureCorridor.Horizontal(route[i] - route[i - 1])) < .9995f ||
                Vector3.Dot(axis, PressureCorridor.Horizontal(route[i + 1] - route[i])) < .9995f) return false;
        }
        center.y = terrain.transform.position.y + terrain.SampleHeight(center);
        var facing = Quaternion.LookRotation(axis);
        if (!TerrainFoundation.Evaluate(terrain, node, center, facing, default, out Vector3 origin, out reason))
            return false;

        Vector3 rear = origin - axis * PortOffset + Vector3.up * PortHeight;
        Vector3 front = origin + axis * PortOffset + Vector3.up * PortHeight;
        var before = new List<Vector3>();
        var after = new List<Vector3> { front };
        for (int i = 0; i < route.Count; i++)
        {
            if (distances[i] < start - .04f) before.Add(route[i]);
            if (distances[i] > finish + .04f) after.Add(route[i]);
        }
        before.Add(rear);
        // Keep the XZ direction of the old route. Its earlier height samples are refitted.
        Vector3[] left = WithoutGradeControls(before), right = WithoutGradeControls(after);
        if (!PressureRoute.FitFloor(terrain, left, out Vector3[] first) ||
            !PressureRoute.FitFloor(terrain, right, out Vector3[] second))
        {
            reason = "Пол узла не стыкуется с коридором при допустимом уклоне";
            return false;
        }
        plan = new DockingInsertion(corridor, origin, facing, first, second);
        reason = string.Empty;
        return true;
    }

    static Vector3[] WithoutGradeControls(List<Vector3> points)
    {
        var result = new List<Vector3> { points[0] };
        for (int i = 1; i < points.Count - 1; i++)
        {
            Vector3 incoming = PressureCorridor.Horizontal(points[i] - points[i - 1]);
            Vector3 outgoing = PressureCorridor.Horizontal(points[i + 1] - points[i]);
            if (Vector3.Dot(incoming, outgoing) < .9995f) result.Add(points[i]);
        }
        result.Add(points[points.Count - 1]);
        return result.ToArray();
    }

    static float FlatLength(Vector3 vector) => new Vector2(vector.x, vector.z).magnitude;
}
