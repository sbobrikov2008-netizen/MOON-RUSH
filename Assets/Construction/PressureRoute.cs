using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal static class PressureRoute
{
    internal readonly struct Anchor
    {
        public readonly BuildingInstance Building;
        public readonly Transform Port;
        public readonly Vector3 Position, Normal;

        public Anchor(BuildingInstance building, Transform port)
        {
            Building = building;
            Port = port;
            Position = port.position;
            Normal = port.forward;
        }

        public bool Exists => Port != null;
    }

    public static Vector3[] Find(Terrain terrain, Anchor source, Anchor target,
        IReadOnlyList<BuildingInstance> buildings, IReadOnlyList<PressureCorridor> corridors, out string failure)
    {
        Vector3[] result = null;
        string reason = Failure;
        var search = Plan(terrain, source, target, buildings, corridors, value => result = value, value => reason = value);
        while (search.MoveNext()) { }
        failure = reason;
        return result;
    }

    internal const string Failure = "Нет проходимого маршрута: проверьте место у шлюзов, препятствия и уклон";
    const int MaximumStatesPerGrid = 1600;
    const int MaximumStatesPerRequest = 4800;

    internal static IEnumerator Plan(Terrain terrain, Anchor source, Anchor target,
        IReadOnlyList<BuildingInstance> buildings, IReadOnlyList<PressureCorridor> corridors, Action<Vector3[]> complete, Action<string> report = null)
    {
        string diagnostic = Failure;
        int diagnosticPriority = 0;
        if (!source.Exists || !target.Exists || source.Building == target.Building ||
            (target.Position - source.Position).sqrMagnitude < .64f)
        {
            report?.Invoke("Выберите два свободных шлюза разных сооружений с зазором не менее 0,8 м");
            complete(null);
            yield break;
        }
        Vector3 outward = source.Normal, inward = target.Normal;
        float sourceLead = DoorLead(source), targetLead = DoorLead(target);
        Vector3 exit = source.Position + outward * sourceLead, entry = target.Position + inward * targetLead;
        Vector3[] best = null;
        int remainingStates = MaximumStatesPerRequest;
        float bestLength = float.PositiveInfinity;
        if (!Clear(source.Position, source.Position + outward * .68f) ||
            !Clear(target.Position, target.Position + inward * .68f))
        {
            report?.Invoke(diagnostic);
            complete(null);
            yield break;
        }
        if (Consider(new[] { source.Position, target.Position }))
        {
            report?.Invoke(string.Empty);
            complete(best);
            yield break;
        }

        foreach (float lead in new[] { 0f, -.45f, 1.75f })
        {
            exit = source.Position + outward * (sourceLead + lead);
            entry = target.Position + inward * (targetLead + lead);
            Vector3 axis = PressureCorridor.Horizontal(entry-exit);
            Vector3 side = Vector3.Cross(Vector3.up,axis);
            Consider(new[] { source.Position,exit,entry,target.Position });
            foreach (Vector3 alignment in new[] { outward, inward })
            {
                Vector3 elbow = exit + alignment * Vector3.Dot(entry-exit,alignment);
                Consider(new[] { source.Position,exit,elbow,entry,target.Position });
            }
            if (best != null) break;
            for (int offset=-4;offset<=4;offset++)
            {
                if(offset==0) continue;
                Vector3 shift=side*offset*6.5f;
                Consider(new[]{source.Position,exit,Vector3.Lerp(exit,entry,.32f)+shift,
                    Vector3.Lerp(exit,entry,.68f)+shift,entry,target.Position});
                yield return null;
                if(best!=null) break;
            }
            if(best!=null) break;
        }
        if (best == null)
        {
            // A nearby obstruction can block the long lead while leaving room to turn early.
            foreach (float extension in new[] { 0f, 1.75f })
            {
                exit = source.Position + outward * (sourceLead + extension);
                entry = target.Position + inward * (targetLead + extension);
                if (!Clear(source.Position, exit) || !Clear(entry, target.Position)) continue;
                foreach (float pitch in new[] { 4.2f, 5.6f })
                {
                    IEnumerator search = Search(pitch);
                    while (search.MoveNext()) yield return null;
                    if (best != null || remainingStates <= 0) break;
                }
                if (best != null || remainingStates <= 0) break;
            }
        }
        if (best == null)
        {
            if (remainingStates <= 0)
                diagnostic += " · Достигнут лимит поиска обхода";
            report?.Invoke(diagnostic);
        }
        else report?.Invoke(string.Empty);
        complete(best);

        void Reject(string reason, int priority = 1)
        {
            if (priority <= diagnosticPriority) return;
            diagnostic = reason;
            diagnosticPriority = priority;
        }

        bool Consider(Vector3[] path)
        {
            var controls = new List<Vector3> { path[0] };
            for (int i=1;i<path.Length;i++)
                if(FlatDistance(controls[controls.Count-1],path[i])>.045f) controls.Add(path[i]);
            for(int i=controls.Count-2;i>0;i--)
                if(Vector3.Dot(PressureCorridor.Horizontal(controls[i]-controls[i-1]),
                    PressureCorridor.Horizontal(controls[i+1]-controls[i]))>.9998f) controls.RemoveAt(i);
            path=controls.ToArray();
            if(path.Length<2) return false;
            if (Vector3.Dot(PressureCorridor.Horizontal(path[1] - path[0]), outward) < .9995f ||
                Vector3.Dot(PressureCorridor.Horizontal(path[path.Length - 2] - path[path.Length - 1]), inward) < .9995f)
                return false;
            float length = 0f;
            for (int i = 1; i < path.Length; i++)
            {
                Vector3 a = path[i - 1], b = path[i];
                float run = FlatDistance(a,b);
                if (run < (path.Length == 2 ? .80f : 2.04f) || (i > 1 && !SmoothTurn(a - path[i - 2], b - a))) return false;
                length += run;
                if (length >= bestLength || !Clear(a,b)) return false;
            }
            if (!PressureNetwork.RouteFits(path, corridors))
            {
                Reject("Повороты маршрута пересекаются или переходы расположены слишком близко");
                return false;
            }
            if (!FitFloor(terrain, path, out var route, (reason, point) =>
            {
                Anchor nearby = FlatDistance(point, source.Position) < FlatDistance(point, target.Position) ? source : target;
                if (FlatDistance(point, nearby.Position) < 6f)
                    Reject($"{reason} у шлюза «{nearby.Port.name}» сооружения «{nearby.Building.Definition.title}»", 3);
                else Reject($"{reason} у точки X {point.x:F1}, Z {point.z:F1}", 2);
            })) return false;
            var curved = PressureCorridor.SmoothCenterline(route, out bool fits);
            if (!fits)
            {
                Reject("Недостаточно места для плавного поворота перехода", 2);
                return false;
            }
            for (int i=1;i<curved.Count;i++)
            {
                if (!Clear(curved[i-1].point,curved[i].point)) return false;
                Vector3 point = curved[i].point;
                Vector3 across = Vector3.Cross(Vector3.up,curved[i].axis) * PressureCorridor.HalfWidth;
                if (Mathf.Max(terrain.SampleHeight(point - across),terrain.SampleHeight(point + across)) +
                    terrain.transform.position.y > point.y + PressureCorridor.Floor - .022f)
                {
                    Reject($"Грунт пересекает пол на повороте у X {point.x:F1}, Z {point.z:F1}", 2);
                    return false;
                }
            }
            best = route;
            bestLength = length;
            return true;
        }

        bool Clear(Vector3 a, Vector3 b)
        {
            Vector3 right = Vector3.Cross(Vector3.up, PressureCorridor.Horizontal(b - a));
            int steps = Mathf.Max(1, Mathf.CeilToInt(FlatDistance(a,b) / .72f));
            for (int i = 0; i <= steps; i++)
            {
                Vector3 point = Vector3.Lerp(a,b,i/(float)steps);
                Vector3 localTerrain = point - terrain.transform.position, size = terrain.terrainData.size;
                if (localTerrain.x < 1.65f || localTerrain.z < 1.65f || localTerrain.x > size.x-1.65f ||
                    localTerrain.z > size.z-1.65f)
                {
                    Reject("Маршрут выходит за границу Terrain");
                    return false;
                }
                if (Mathf.Abs(terrain.SampleHeight(point + right*1.47f) -
                    terrain.SampleHeight(point - right*1.47f)) > 1.23f)
                {
                    Reject($"Поперечный уклон грунта у X {point.x:F1}, Z {point.z:F1} слишком велик", 2);
                    return false;
                }
                foreach (BuildingInstance building in buildings)
                {
                    if (building == null || building.Kind == BuildingKind.PressureCorridor) continue;
                    if ((building == source.Building && AtPort(point, source, sourceLead)) ||
                        (building == target.Building && AtPort(point, target, targetLead))) continue;
                    Vector3 local = building.transform.InverseTransformPoint(point);
                    BoxCollider collider = building.GetComponent<BoxCollider>();
                    Vector3 centered = collider == null ? local : local-collider.center;
                    if (collider != null && building.Kind != BuildingKind.LandingPad)
                    {
                        if (Mathf.Abs(centered.x) < collider.size.x*.5f+1.55f &&
                            Mathf.Abs(centered.z) < collider.size.z*.5f+1.55f)
                            return BlockedBy(building);
                    }
                    else if (FlatDistance(point,building.transform.position) < building.Definition.footprintRadius+1.55f)
                        return BlockedBy(building);
                    Rect ramp = building.Kind == BuildingKind.LandingPad ? LunarLandingPad.RampFootprint :
                        building.Kind == BuildingKind.CargoHangar ? CargoHangarRamp.Footprint : default;
                    if (ramp.width > 0f && local.x > ramp.xMin-1.55f && local.x < ramp.xMax+1.55f &&
                        local.z > ramp.yMin-1.55f && local.z < ramp.yMax+1.55f)
                    {
                        Reject($"Маршрут пересекает пандус сооружения «{building.Definition.title}»", 3);
                        return false;
                    }
                }
                foreach (PressureCorridor corridor in corridors)
                    if (corridor != null && corridor.Occupies(point,3.16f))
                    {
                        string from = corridor.First != null ? corridor.First.Definition.title : corridor.name;
                        string to = corridor.Second != null ? corridor.Second.Definition.title : corridor.name;
                        Reject($"Маршрут пересекает переход «{from} — {to}». Здесь нужен соединительный узел", 3);
                        return false;
                    }
            }
            return true;
        }

        bool BlockedBy(BuildingInstance building)
        {
            bool endpoint = building == source.Building || building == target.Building;
            Reject(endpoint
                ? $"Поворот у шлюза «{(building == source.Building ? source.Port.name : target.Port.name)}» упирается в корпус или фундамент «{building.Definition.title}»"
                : $"Маршрут блокирует сооружение «{building.Definition.title}»", endpoint ? 2 : 4);
            return false;
        }

        IEnumerator Search(float pitch)
        {
            Vector3 east = outward, north = Vector3.Cross(Vector3.up, east);
            var directions = new (int x, int z)[] { (1,0),(1,1),(0,1),(-1,1),(-1,0),(-1,-1),(0,-1),(1,-1) };
            var start = (x: 0, z: 0, heading: 0);
            var costs = new Dictionary<(int x, int z, int heading),float> { [start] = 0f };
            var parents = new Dictionary<(int x, int z, int heading),(int x, int z, int heading)>();
            var closed = new HashSet<(int x, int z, int heading)>();
            var frontier = new SortedSet<(float score,int serial,int x,int z,int heading)> { (0f,0,0,0,0) };
            int serial = 1;
            int work = 0;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var visibility = new Dictionary<(int,int,int,int),bool>();
            float limit = FlatDistance(exit,entry)+62f;
            while (frontier.Count > 0 && closed.Count < MaximumStatesPerGrid && remainingStates > 0)
            {
                if (++work >= 24 || clock.Elapsed.TotalMilliseconds >= 2.4)
                {
                    yield return null;
                    work = 0;
                    clock.Restart();
                }
                var item = frontier.Min;
                frontier.Remove(item);
                var state = (item.x,item.z,item.heading);
                if (!closed.Add(state)) continue;
                remainingStates--;
                Vector3 current = World(state.x,state.z);
                Vector3 approach = entry-current;
                float remaining = FlatDistance(current, entry);
                if (remaining >= 3.45f && remaining < 39f && SmoothTurn(Heading(state.heading),approach) &&
                    SmoothTurn(approach,-inward) && Clear(current,entry))
                {
                    var route = new List<Vector3> { target.Position,entry,current };
                    var trace = state;
                    while (parents.TryGetValue(trace,out var prior))
                    {
                        trace = prior;
                        route.Add(World(trace.x,trace.z));
                    }
                    route.Add(source.Position);
                    route.Reverse();
                    for (int i = route.Count-2; i>0; i--)
                        if (Vector3.Dot(PressureCorridor.Horizontal(route[i]-route[i-1]),
                            PressureCorridor.Horizontal(route[i+1]-route[i])) > .9998f) route.RemoveAt(i);
                    if (Consider(route.ToArray())) yield break;
                }

                for (int turn=-2; turn<=2; turn++)
                {
                    int direction=(state.heading+turn+8)%8;
                    var move=directions[direction];
                    var next=(state.x+move.x,state.z+move.z,direction);
                    if (closed.Contains(next)) continue;
                    Vector3 destination=World(next.Item1,next.Item2);
                    float estimate=FlatDistance(destination,entry);
                    if (estimate>limit) continue;
                    var edge=state.x < next.Item1 || state.x==next.Item1 && state.z<next.Item2
                        ? (state.x,state.z,next.Item1,next.Item2) : (next.Item1,next.Item2,state.x,state.z);
                    if(!visibility.TryGetValue(edge,out bool open))
                        visibility.Add(edge,open=Clear(current,destination));
                    if(!open) continue;
                    float grade=Mathf.Abs(terrain.SampleHeight(current)-terrain.SampleHeight(destination));
                    float run=FlatDistance(current,destination);
                    if (grade>run*.35f) continue;
                    float cost=costs[state]+run+Mathf.Abs(turn)*1.15f+grade*3f;
                    if (costs.TryGetValue(next,out float old) && old<=cost) continue;
                    costs[next]=cost;
                    parents[next]=state;
                    frontier.Add((cost+estimate,serial++,next.Item1,next.Item2,direction));
                }
            }
            Vector3 World(int x,int z) => exit+(east*x+north*z)*pitch;
            Vector3 Heading(int i) => east*directions[i].x+north*directions[i].z;
        }

        static bool SmoothTurn(Vector3 a, Vector3 b) =>
            Vector3.Dot(PressureCorridor.Horizontal(a),PressureCorridor.Horizontal(b)) >= -.035f;

        static bool AtPort(Vector3 point, Anchor anchor, float lead)
        {
            Vector3 offset = point - anchor.Position;
            offset.y=0f;
            float along=Vector3.Dot(offset,anchor.Normal);
            float permitted = Mathf.Clamp(.34f + Mathf.Max(0f, along - (lead - 2.1f)) * .55f, .34f, 1.38f);
            return along >= -.04f && along <= lead + .5f &&
                (offset - anchor.Normal * along).sqrMagnitude <= permitted * permitted;
        }
    }

    internal static float DoorLead(Anchor anchor)
    {
        // The selection collider can extend well beyond the real door (e.g. under a roof overhang).
        // Reserve the narrow docking throat up to the outer face of that collider.
        if (anchor.Building == null) return 2.75f;
        BoxCollider box = anchor.Building.GetComponent<BoxCollider>();
        if (box == null) return 2.75f;
        Transform root = anchor.Building.transform;
        Vector3 start = root.InverseTransformPoint(anchor.Position) - box.center;
        Vector3 localDirection = root.InverseTransformPoint(anchor.Position + anchor.Normal) -
            root.InverseTransformPoint(anchor.Position);
        float reach = float.PositiveInfinity;
        if (Mathf.Abs(localDirection.x) > .001f)
            reach = Mathf.Min(reach, (Mathf.Sign(localDirection.x) * (box.size.x * .5f + 1.55f) - start.x) /
                localDirection.x);
        if (Mathf.Abs(localDirection.z) > .001f)
            reach = Mathf.Min(reach, (Mathf.Sign(localDirection.z) * (box.size.z * .5f + 1.55f) - start.z) /
                localDirection.z);
        return float.IsInfinity(reach) ? 2.75f : Mathf.Max(2.75f, reach + .65f);
    }

    static float FlatDistance(Vector3 a,Vector3 b) => new Vector2(a.x-b.x,a.z-b.z).magnitude;

    // Fit the lowest profile that clears the full deck and can still meet both fixed door sills.
    internal static bool FitFloor(Terrain terrain, Vector3[] route, out Vector3[] fitted,
        Action<string, Vector3> reject = null)
    {
        const float maximumGrade = .18f;
        const float groundMargin = .23f;
        var points = new List<Vector3> { route[0] };
        var lower = new List<float> { float.NegativeInfinity };
        var upper = new List<float> { float.PositiveInfinity };
        var corners = new List<int>();
        var station = new List<float> { 0f };
        float totalLength = 0f;
        for (int i = 1; i < route.Length; i++) totalLength += FlatDistance(route[i - 1], route[i]);
        float traveled = 0f;
        fitted = null;
        for (int span = 1; span < route.Length; span++)
        {
            Vector3 a = route[span - 1], b = route[span];
            Vector3 axis = PressureCorridor.Horizontal(b - a);
            if (span > 1 && Vector3.Dot(axis, PressureCorridor.Horizontal(a - route[span - 2])) < .9995f)
                corners.Add(points.Count - 1);
            Vector3 right = Vector3.Cross(Vector3.up, axis);
            float horizontalRun = FlatDistance(a, b);
            int count = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / .72f));
            for (int step = 0; step <= count; step++)
            {
                Vector3 point = Vector3.Lerp(a, b, step / (float)count);
                float high = float.NegativeInfinity, low = float.PositiveInfinity;
                for (int across = -2; across <= 2; across++)
                {
                    Vector3 sample = point + right * (across * .78f);
                    Vector3 local = sample - terrain.transform.position;
                    Vector3 size = terrain.terrainData.size;
                    float u = local.x / size.x, v = local.z / size.z;
                    int holes = terrain.terrainData.holesResolution;
                    if (u < 0f || u >= 1f || v < 0f || v >= 1f ||
                        terrain.terrainData.IsHole((int)(u * holes), (int)(v * holes)))
                    {
                        reject?.Invoke("Пол вышел за пределы Terrain или попал в отверстие", sample);
                        return false;
                    }
                    float y = terrain.transform.position.y + terrain.SampleHeight(sample);
                    high = Mathf.Max(high, y);
                    low = Mathf.Min(low, y);
                }
                float portDistance = Mathf.Min(FlatDistance(point, route[0]), FlatDistance(point, route[route.Length - 1]));
                float clearance = Mathf.Lerp(.035f, groundMargin, Mathf.Clamp01(portDistance / 1.7f));
                float minimum = high - PressureCorridor.Floor + clearance;
                // The old world-wide cap rejected even a valid sill on a raised foundation.
                // Extend it only along the approach required to descend from each fixed door.
                float distance = traveled + horizontalRun * step / count;
                float level = Mathf.Max(route[0].y - distance * maximumGrade,
                    route[route.Length - 1].y - (totalLength - distance) * maximumGrade);
                float normalCeiling = low - PressureCorridor.Floor + 1.52f;
                float accessCeiling = Mathf.Min(low - PressureCorridor.Floor + 3.45f, level + .39f);
                float maximum = Mathf.Max(normalCeiling, accessCeiling);
                if (step == 0)
                {
                    int last = points.Count - 1;
                    lower[last] = Mathf.Max(lower[last], minimum);
                    upper[last] = Mathf.Min(upper[last], maximum);
                }
                else
                {
                    points.Add(point);
                    station.Add(distance);
                    lower.Add(minimum);
                    upper.Add(maximum);
                }
            }
            traveled += horizontalRun;
        }

        int lastIndex = points.Count - 1;
        if (points[0].y < lower[0] || points[0].y > upper[0] + .001f ||
            points[lastIndex].y < lower[lastIndex] || points[lastIndex].y > upper[lastIndex] + .001f)
        {
            int blocked = points[0].y < lower[0] || points[0].y > upper[0] + .001f ? 0 : lastIndex;
            reject?.Invoke("Отметка порога не согласуется с высотой грунта под стыком", points[blocked]);
            return false;
        }
        upper[0] = lower[0] = points[0].y;
        upper[lastIndex] = lower[lastIndex] = points[lastIndex].y;
        // Turns need a level collar wide enough to carry the shared curved shell.
        for (int pass = 0; pass <= corners.Count; pass++)
        {
            foreach (int corner in corners)
            {
                int left = corner, right = corner;
                while (left > 1 && Run(left - 1, corner) < 2.70f) left--;
                while (right < lastIndex - 1 && Run(right + 1, corner) < 2.70f) right++;
                float level = lower[corner];
                for (int i = left; i <= right; i++) level = Mathf.Max(level, lower[i]);
                for (int i = left; i <= right; i++) lower[i] = level;
            }
            for (int i = 1; i <= lastIndex; i++)
                lower[i] = Mathf.Max(lower[i], lower[i - 1] - Run(i, i - 1) * maximumGrade);
            for (int i = lastIndex - 1; i >= 0; i--)
                lower[i] = Mathf.Max(lower[i], lower[i + 1] - Run(i, i + 1) * maximumGrade);
        }
        for (int i = 0; i <= lastIndex; i++)
        {
            if (lower[i] > upper[i] + .001f)
            {
                reject?.Invoke("Превышен допустимый уклон пола или высота опор", points[i]);
                return false;
            }
            Vector3 point = points[i];
            point.y = lower[i];
            points[i] = point;
        }
        var compact = new List<Vector3> { points[0] };
        for (int i = 1; i < lastIndex; i++)
            if (Vector3.Cross((points[i] - points[i - 1]).normalized, (points[i + 1] - points[i]).normalized)
                .sqrMagnitude > .0000001f) compact.Add(points[i]);
        compact.Add(points[lastIndex]);
        fitted = compact.ToArray();
        return true;

        float Run(int a, int b) => Mathf.Abs(station[a] - station[b]);

        static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
    }
}
