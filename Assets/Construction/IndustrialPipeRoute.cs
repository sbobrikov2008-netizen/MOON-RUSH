using System.Collections.Generic;
using UnityEngine;

internal static class IndustrialPipeRoute
{
    public static Vector3[] Plan(Terrain terrain, BuildingInstance source, BuildingInstance target,
        ColonyIndustry.Resource cargo, IReadOnlyList<BuildingInstance> buildings)
    {
        Vector3 start = source.transform.TransformPoint(ColonyIndustry.Socket(source.Kind, true, cargo));
        Vector3 end = target.transform.TransformPoint(ColonyIndustry.Socket(target.Kind, false, cargo));
        Vector3 exitDirection = source.transform.right;
        Vector3 entryDirection = target.transform.right * (target.Kind == BuildingKind.FuelWorks &&
            cargo == ColonyIndustry.Resource.Water ? 1f : -1f);
        Vector3 exit = Lead(source, start, exitDirection), entry = Lead(target, end, entryDirection);
        Vector3 direction = PressureCorridor.Horizontal(entry - exit);
        Vector3 side = Vector3.Cross(Vector3.up, direction);
        Vector3[] best = null;
        float shortest = float.PositiveInfinity;
        for (int branch = -6; branch <= 6; branch++)
        {
            Vector3 offset = side * branch * 6f;
            Vector3[] path = branch == 0 ? new[] { start, exit, entry, end } :
                new[] { start, exit, Vector3.Lerp(exit, entry, .22f) + offset,
                    Vector3.Lerp(exit, entry, .78f) + offset, entry, end };
            var points = new List<Vector3> { start };
            float length = 0f;
            bool valid = true;
            for (int i = 1; i < path.Length && valid; i++)
            {
                float run = Vector3.Distance(path[i - 1], path[i]);
                int samples = Mathf.Max(1, Mathf.CeilToInt(run / .85f));
                length += run;
                for (int step = 1; step <= samples; step++)
                {
                    Vector3 point = Vector3.Lerp(path[i - 1], path[i], step / (float)samples);
                    if (!Clear(point, i == 1, i == path.Length - 1))
                    {
                        valid = false;
                        break;
                    }
                    float y = terrain.transform.position.y + terrain.SampleHeight(point);
                    point.y = y + .39f;
                    points.Add(i == path.Length - 1 && step == samples ? end : point);
                }
            }
            if (!valid || length >= shortest) continue;
            shortest = length;
            best = points.ToArray();
        }
        return best;

        bool Clear(Vector3 point, bool lead, bool arrival)
        {
            Vector3 local = point - terrain.transform.position, size = terrain.terrainData.size;
            if (local.x < .2f || local.z < .2f || local.x > size.x - .2f || local.z > size.z - .2f) return false;
            foreach (BuildingInstance building in buildings)
            {
                if (building == null || (building == source && lead) || (building == target && arrival)) continue;
                if (building.Kind == BuildingKind.PressureCorridor) continue;
                Vector3 delta = point - building.transform.position;
                delta.y = 0f;
                float radius = building.Definition.footprintRadius + .62f;
                if (delta.sqrMagnitude < radius * radius) return false;
                Rect ramp = building.Kind == BuildingKind.LandingPad ? LunarLandingPad.RampFootprint :
                    building.Kind == BuildingKind.CargoHangar ? CargoHangarRamp.Footprint : default;
                Vector3 relative = building.transform.InverseTransformPoint(point);
                if (ramp.width > 0f && relative.x >= ramp.xMin && relative.x <= ramp.xMax &&
                    relative.z >= ramp.yMin && relative.z <= ramp.yMax) return false;
            }
            return true;
        }

        static Vector3 Lead(BuildingInstance item, Vector3 socket, Vector3 direction)
        {
            float distance = Vector3.Dot(socket - item.transform.position, direction);
            return socket + direction * Mathf.Max(.7f, item.Definition.footprintRadius + .85f - distance);
        }
    }
}
