using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class LunarColonyOperations : MonoBehaviour
{
    sealed class Agent
    {
        public LunarVehicleModels.Model Model;
        public BuildingInstance Room, Hangar, Airlock;
        public readonly Queue<Vector3> Route = new Queue<Vector3>();
        public float Next, Cycle, NextClearanceCheck;
        public int Trips;
        public bool HasWork, Docking, OnRamp, HasDestination;
        public Vector3 Destination;
        public float Speed, Turn, WorkTime, BestDistance, Gait;
        public int Task, Attempts, StalledLegs;
        public Transform Sample;
    }

    Terrain terrain;
    List<BuildingInstance> buildings;
    List<PressureCorridor> corridors;
    LunarVehicleModels models;
    Action collect;
    readonly Agent[] people=new Agent[2], suited=new Agent[2], rovers=new Agent[2];
    readonly HashSet<BuildingInstance> launched=new HashSet<BuildingInstance>();
    readonly List<PressureCorridor> knownCorridors=new List<PressureCorridor>();
    BuildingInstance home, launchPad, openedAirlock;
    float closeAirlockAt, launchTime;
    (Transform root, Transform exhaust, Light glow, Transform[] legs) rocket;
    int planFrame=-1;
    const float RoverClearance=3.79f, PersonClearance=.39f, RoverRampExit=32f;
    readonly Collider[] movementHits=new Collider[48];
    static readonly float[] AlternativeTurns={0f,-24f,24f,-48f,48f,-72f,72f,-108f,108f};
    const float Countdown=5f, Descent=12f, SurfaceStay=30f, Ascent=10f;

    internal void Initialize(Terrain surface,List<BuildingInstance> structures,List<PressureCorridor> connections,
        Func<string,Color,Material> materials,Action sample)
    {
        terrain=surface;
        buildings=structures;
        corridors=connections;
        models=new LunarVehicleModels(materials);
        collect=sample;
    }

    void Update()
    {
        if (terrain==null) return;
        TickRocket();
        BuildingInstance habitat=Find(BuildingKind.Habitat);
        if (home!=habitat || TopologyChanged())
        {
            home=habitat;
            knownCorridors.Clear();
            knownCorridors.AddRange(corridors);
            for(int i=0;i<people.Length;i++) ResetCrew(i);
        }
        if (home!=null)
            for(int i=0;i<people.Length;i++) TickCrew(i);
        BuildingInstance anchor=home!=null?home:Find(BuildingKind.LandingPad);
        if(anchor==null) anchor=Find(BuildingKind.CommandPost);
        if(anchor==null) anchor=Find(BuildingKind.AirlockCapsule);
        if(anchor!=null)
            for(int i=0;i<2;i++) TickSurface(suited,i,anchor,false);
        BuildingInstance garage=Find(BuildingKind.CargoHangar);
        for(int i=0;i<rovers.Length;i++)
        {
            if(rovers[i]!=null && rovers[i].Hangar!=garage)
            {
                rovers[i].Model.Root.gameObject.SetActive(false);
                Destroy(rovers[i].Model.Root.gameObject);
                rovers[i]=null;
            }
            if(garage!=null) TickSurface(rovers,i,garage,true);
        }
        if(openedAirlock!=null && Time.time>=closeAirlockAt)
        {
            if(openedAirlock.TryGetComponent(out LunarBaseModule module) && module.SurfaceDoorOpen)
                module.ToggleSurfaceDoor();
            openedAirlock=null;
        }
    }

    BuildingInstance Find(BuildingKind kind)
    {
        foreach(BuildingInstance building in buildings)
            if(building!=null && building.Kind==kind) return building;
        return null;
    }

    bool TopologyChanged()
    {
        if(knownCorridors.Count!=corridors.Count) return true;
        for(int i=0;i<corridors.Count;i++)
            if(knownCorridors[i]!=corridors[i]) return true;
        foreach(Agent person in people)
            if(person!=null && !buildings.Contains(person.Room)) return true;
        return false;
    }

    void ResetCrew(int index)
    {
        Agent person=people[index];
        if(home==null)
        {
            if(person!=null) person.Model.Root.gameObject.SetActive(false);
            return;
        }
        if(person==null) people[index]=person=new Agent{Model=models.Person(transform,false,index)};
        person.Model.Root.gameObject.SetActive(true);
        person.Room=home;
        person.Route.Clear();
        person.HasWork=false;
        person.Next=Time.time+index*2.5f;
        person.Model.Root.position=Local(home,index==0?2.28f:3.30f,1.47f,7.72f);
    }

    void TickCrew(int index)
    {
        Agent agent=people[index];
        if(agent.Route.Count>0)
        {
            Step(agent,1.35f,false,false);
            if (agent.Route.Count==0) agent.Next=Time.time+9f;
            return;
        }
        agent.HasWork=agent.HasWork && agent.Room!=null && agent.Room.HasPower;
        Animate(agent,0f,false,false);
        if(Time.time<agent.Next) return;
        agent.HasWork=false;
        var path=FindWorkRoute(agent.Room,agent.Trips++%2==0?BuildingKind.ScienceLab:BuildingKind.Greenhouse);
        if(path.Count==0 && agent.Room==home) path=FindWorkRoute(agent.Room,BuildingKind.DockingNode);
        if(path.Count==0 && agent.Room!=home) path=FindWorkRoute(agent.Room,BuildingKind.Habitat);
        if(path.Count==0)
        {
            if(agent.Room!=home || !PatrolConnectedCorridor(agent,index))
                agent.Route.Enqueue(Local(agent.Room,index==0?2.28f:3.30f,1.47f,agent.Trips%2==0?6.2f:8.1f));
            agent.Next=Time.time+3.5f;
            return;
        }
        foreach(var leg in path)
        {
            Transform start=leg.forward?leg.corridor.StartPort:leg.corridor.EndPort;
            Transform end=leg.forward?leg.corridor.EndPort:leg.corridor.StartPort;
            BuildingInstance target=leg.forward?leg.corridor.Second:leg.corridor.First;
            AddInterior(agent,agent.Room,start,true,index);
            var line=leg.corridor.Centerline;
            for(int i=0;i<line.Count;i++)
            {
                int j=leg.forward?i:line.Count-i-1;
                agent.Route.Enqueue(CorridorLane(line[j],index));
            }
            AddInterior(agent,target,end,false,index);
            agent.Room=target;
        }
        agent.HasWork=agent.Room.Kind==BuildingKind.ScienceLab || agent.Room.Kind==BuildingKind.Greenhouse;
        agent.Task=agent.Room.Kind==BuildingKind.Greenhouse?2:1;
        agent.Next=Time.time+agent.Route.Count*.35f+15f;
    }

    bool PatrolConnectedCorridor(Agent agent, int index)
    {
        foreach(PressureCorridor corridor in corridors)
        {
            bool forward=corridor.First==home;
            if(!forward && corridor.Second!=home) continue;
            Transform port=forward?corridor.StartPort:corridor.EndPort;
            if(!Walkable(home,port)) continue;
            var line=corridor.Centerline;
            if (line.Count < 2) continue;
            AddInterior(agent,home,port,true,index);
            int last=0;
            float distance=0f;
            for(int i=0;i<line.Count;i++)
            {
                Vector3 point=At(i);
                if(i>0) distance+=Vector3.Distance(point,At(i-1));
                if(distance>7f && i>1) break;
                agent.Route.Enqueue(point);
                last=i;
            }
            for(int i=last-1;i>=0;i--) agent.Route.Enqueue(At(i));
            AddInterior(agent,home,port,false,index);
            return true;
            Vector3 At(int i) => CorridorLane(line[forward?i:line.Count-1-i],index);
        }
        return false;
    }

    List<(PressureCorridor corridor,bool forward)> FindWorkRoute(BuildingInstance start,BuildingKind goal)
    {
        var result=new List<(PressureCorridor corridor,bool forward)>();
        if(start.Kind==goal) return result;
        var seen=new HashSet<BuildingInstance>{start};
        var links=new Dictionary<BuildingInstance,(BuildingInstance previous,PressureCorridor corridor,bool forward)>();
        var queue=new Queue<BuildingInstance>();
        queue.Enqueue(start);
        while(queue.Count>0)
        {
            BuildingInstance room=queue.Dequeue();
            if(room.Kind==goal && ((goal!=BuildingKind.ScienceLab && goal!=BuildingKind.Greenhouse) || room.HasPower))
            {
                while(room!=start)
                {
                    var link=links[room];
                    result.Add((link.corridor,link.forward));
                    room=link.previous;
                }
                result.Reverse();
                return result;
            }
            foreach(PressureCorridor corridor in corridors)
            {
                bool forward=corridor.First==room;
                if(!forward && corridor.Second!=room) continue;
                BuildingInstance other=forward?corridor.Second:corridor.First;
                Transform entrance=forward?corridor.StartPort:corridor.EndPort;
                Transform exit=forward?corridor.EndPort:corridor.StartPort;
                if(!Walkable(room,entrance) || !Walkable(other,exit) || !seen.Add(other)) continue;
                links.Add(other,(room,corridor,forward));
                queue.Enqueue(other);
            }
        }
        return result;
    }

    static bool Walkable(BuildingInstance room,Transform port)
    {
        if(room==null || port==null) return false;
        switch(room.Kind)
        {
            case BuildingKind.DockingNode:
            case BuildingKind.AirlockCapsule:
            case BuildingKind.ScienceLab: return true;
            case BuildingKind.Habitat:
            case BuildingKind.Greenhouse: return true;
            default: return false;
        }
    }

    void AddInterior(Agent agent,BuildingInstance room,Transform port,bool leaving,int index)
    {
        var points=new List<Vector3>();
        bool rear=room.transform.InverseTransformPoint(port.position).z<0f;
        switch(room.Kind)
        {
            case BuildingKind.Habitat:
                points.Add(Local(room,index==0?2.28f:3.30f,1.47f,7.72f));
                if(rear)
                {
                    points.Add(Local(room,-3.38f,1.47f,7.2f));
                    points.Add(Local(room,-3.38f,1.47f,-7.07f));
                    points.Add(Local(room,0f,1.47f,-8.59f));
                }
                else
                {
                    points.Add(Local(room,0f,1.47f,9.1f));
                    points.Add(Local(room,0f,.83f,11.3f));
                }
                break;
            case BuildingKind.ScienceLab:
                points.Add(Local(room,index==0?-4.42f:4.42f,.65f,-1.38f));
                points.Add(Local(room,0f,.65f,0f));
                break;
            case BuildingKind.Greenhouse:
                points.Add(Local(room,index==0?-.75f:.75f,.73f,4.3f));
                points.Add(Local(room,0f,.73f,rear?-8.7f:8.7f));
                break;
            default:
                points.Add(Local(room,index==0?-.48f:.48f,.62f,0f));
                break;
        }
        if(!leaving) points.Reverse();
        foreach(Vector3 point in points) agent.Route.Enqueue(point);
    }

    static Vector3 CorridorLane((Vector3 point,Vector3 axis) section,int index) =>
        section.point+Vector3.up*PressureCorridor.Floor+
        Vector3.Cross(Vector3.up,section.axis)*(index==0?-.47f:.47f);

    void TickSurface(Agent[] group,int index,BuildingInstance anchor,bool rover)
    {
        Agent agent=group[index];
        if(agent==null)
        {
            if(rover)
            {
                if(index>0 && (group[0]==null || !RampAvailable(anchor,group[0]) ||
                    Vector3.Distance(group[0].Model.Root.position,Local(anchor,0f,0f,RoverRampExit))<8.6f)) return;
                agent=group[index]=new Agent{Model=models.Rover(transform),Hangar=anchor};
                agent.Model.Root.SetPositionAndRotation(Local(anchor,0f,.62f,6.1f),anchor.transform.rotation);
                agent.Sample=models.Sample(agent.Model.Tool);
                BuildRampRoute(agent,anchor,index,false);
                return;
            }
            BuildingInstance airlock=Find(BuildingKind.AirlockCapsule);
            if(airlock!=null && TryExitAirlock(airlock,index,out Vector3 spawn))
            {
                agent=group[index]=new Agent{Model=models.Person(transform,true,index),Airlock=airlock};
                agent.Model.Root.position=spawn;
                for(int i=1;i<=8;i++) agent.Route.Enqueue(AirlockStep(airlock,index,i));
                agent.OnRamp=true;
                return;
            }
            for(int i=0;i<12;i++)
            {
                Quaternion turn=Quaternion.Euler(0f,index*91f+i*30f,0);
                Vector3 candidate=Grounded(anchor.transform.position+turn*Vector3.forward*(anchor.Definition.footprintRadius+7f));
                if(!IsOutsideClear(candidate,turn*Vector3.forward,false,null)) continue;
                agent=group[index]=new Agent{Model=models.Person(transform,true,index)};
                agent.Model.Root.position=candidate;
                break;
            }
            if(agent==null) return;
        }
        if(agent.OnRamp && !rover && agent.Airlock==null)
        {
            agent.OnRamp=false;
            agent.Route.Clear();
            agent.Model.Root.position=Grounded(agent.Model.Root.position);
        }
        if(agent.Route.Count>0)
        {
            if (Time.time >= agent.NextClearanceCheck)
            {
                agent.NextClearanceCheck = Time.time + .14f;
                Vector3 next=agent.Route.Peek();
                Vector3 heading=PressureCorridor.Horizontal(next-agent.Model.Root.position);
                BuildingInstance rampOwner=agent.OnRamp?(rover?agent.Hangar:agent.Airlock):null;
                if (!IsOutsideClear(next,heading,rover,rampOwner))
                {
                    if(!agent.OnRamp) agent.Route.Clear();
                    agent.Speed=0f;
                    agent.Next=Time.time+.58f+index*.34f;
                    return;
                }
            }
            Step(agent,rover?4.65f:1.24f,true,rover);
            if(agent.Route.Count==0)
            {
                agent.OnRamp=false;
                agent.Next=Time.time+(agent.Docking?7f:.14f);
            }
            return;
        }

        agent.Speed=Mathf.MoveTowards(agent.Speed,0f,Time.deltaTime*3.8f);
        Animate(agent,0f,true,rover);
        if(Time.time<agent.Next || planFrame==Time.frameCount) return;
        if(agent.HasWork)
        {
            if(agent.Task==0) collect();
            agent.HasWork=false;
            if(agent.Sample!=null) agent.Sample.gameObject.SetActive(false);
        }
        if(agent.Docking)
        {
            agent.Docking=false;
            BuildRampRoute(agent,agent.Hangar,index,false);
            return;
        }

        Vector3 current=agent.Model.Root.position;
        if(!rover && agent.Trips>=2)
        {
            BuildingInstance airlock=Find(BuildingKind.AirlockCapsule);
            if(airlock!=null && airlock!=agent.Airlock && !agent.HasDestination)
            {
                Vector3 entrance=Grounded(Local(airlock,index==0?-.6f:.6f,0f,8.5f));
                agent.Destination=entrance;
                agent.BestDistance=Vector3.Distance(current,entrance);
                agent.StalledLegs=0;
                agent.HasDestination=true;
                agent.Task=3;
            }
            if(airlock!=null && agent.Task==3 && Vector3.Distance(current,agent.Destination)<1.4f &&
                TryExitAirlock(airlock,index,out Vector3 inside))
            {
                agent.Airlock=airlock;
                agent.HasDestination=false;
                agent.OnRamp=true;
                for(int i=8;i>=0;i--) agent.Route.Enqueue(AirlockStep(airlock,index,i));
                agent.Route.Enqueue(inside);
                for(int i=1;i<=8;i++) agent.Route.Enqueue(AirlockStep(airlock,index,i));
                agent.Route.Enqueue(agent.Destination);
                closeAirlockAt=Time.time+15f;
                return;
            }
        }
        if(rover && agent.Task==4 && agent.HasDestination && Vector3.Distance(current,agent.Destination)<1.65f)
        {
            if(!RampAvailable(agent.Hangar,agent))
            {
                agent.Next=Time.time+.5f+index*.15f;
                return;
            }
            agent.HasDestination=false;
            agent.Docking=true;
            agent.Trips++;
            BuildRampRoute(agent,agent.Hangar,index,true);
            return;
        }
        if(agent.HasDestination && Vector3.Distance(current,agent.Destination)<(rover?3.4f:1.45f) && agent.Task<3)
        {
            agent.HasDestination=false;
            agent.Trips++;
            agent.HasWork=true;
            agent.WorkTime=Time.time;
            agent.Next=Time.time+(rover?6f:4.1f);
            return;
        }
        if(!agent.HasDestination)
        {
            if(rover && agent.Trips%5==4)
            {
                agent.Task=4;
                agent.Destination=Grounded(Local(agent.Hangar,index==0?-9.5f:9.5f,0f,RoverRampExit+3.5f));
            }
            else
            {
                agent.Task=(agent.Trips+index)%3;
                agent.Destination=SurveyDestination(agent,anchor,index,rover);
            }
            agent.HasDestination=true;
            agent.Attempts=0;
            agent.StalledLegs=0;
            agent.BestDistance=Vector3.Distance(current,agent.Destination);
        }
        float remaining=Vector3.Distance(current,agent.Destination);
        if(remaining<agent.BestDistance-1.1f)
        {
            agent.BestDistance=remaining;
            agent.StalledLegs=0;
        }
        else if(++agent.StalledLegs>8)
        {
            agent.HasDestination=false;
            agent.Trips++;
            agent.Next=Time.time+.45f;
            return;
        }
        planFrame=Time.frameCount;
        if(FindStep(agent,agent.Destination,rover))
        {
            agent.Attempts=0;
            return;
        }
        agent.Next=Time.time+.45f+index*.21f;
        if(++agent.Attempts>=5)
        {
            agent.HasDestination=false;
            agent.Trips++;
        }
    }

    Vector3 SurveyDestination(Agent agent, BuildingInstance anchor,int index,bool rover)
    {
        Vector3 current=agent.Model.Root.position;
        if(agent.Task==1)
        {
            // Approach rotating choices of equipment from its service side.
            int available=0;
            foreach(BuildingInstance building in buildings)
                if(building!=null && building!=anchor && building.Kind!=BuildingKind.PressureCorridor) available++;
            int skip=available>0?(agent.Trips/3+index)%available:0;
            foreach(BuildingInstance building in buildings)
            {
                if(building==null || building==anchor || building.Kind==BuildingKind.PressureCorridor) continue;
                if(skip-->0) continue;
                Vector3 direction=PressureCorridor.Horizontal(current-building.transform.position);
                return Grounded(building.transform.position+direction*(building.Definition.footprintRadius+(rover?4f:2f)));
            }
        }
        Vector3 origin=terrain.transform.position, size=terrain.terrainData.size;
        int sector=agent.Trips*7+index*13+(rover?9:3);
        // Low-discrepancy sampling gives different sectors and distances, including crater floors.
        float x=Mathf.Repeat(.23f+sector*.61803399f,1f);
        float z=Mathf.Repeat(.61f+sector*.41421356f,1f);
        Vector3 region=origin+new Vector3(Mathf.Lerp(25f,size.x-25f,x),0f,Mathf.Lerp(25f,size.z-25f,z));
        if(agent.Task==0)
        {
            Vector3 best=Grounded(region);
            for(int i=0;i<4;i++)
            {
                Vector3 sample=Grounded(Vector3.Lerp(current,region,.25f+i*.16f));
                if(sample.y<best.y) best=sample;
            }
            region=best;
        }
        return Grounded(region);
    }

    Vector3 AirlockStep(BuildingInstance airlock,int index,int step)
    {
        float z=4.1f+step*.53f;
        Vector3 point=Local(airlock,index==0?-.6f:.6f,Mathf.Max(.02f,.62f-(z-5.5f)*.25f),z);
        point.y=Mathf.Max(point.y,Ground(point));
        return point;
    }

    bool TryExitAirlock(BuildingInstance airlock,int index,out Vector3 inside)
    {
        inside=Local(airlock,index==0?-.6f:.6f,.62f,4.1f);
        if(!airlock.TryGetComponent(out LunarBaseModule module) || module.SurfaceDoor==null) return false;
        if(!module.SurfaceDoorOpen)
        {
            module.ToggleSurfaceDoor();
            openedAirlock=airlock;
        }
        if(openedAirlock==airlock) closeAirlockAt=Time.time+9f;
        return true;
    }

    bool RampAvailable(BuildingInstance hangar,Agent applicant)
    {
        foreach(Agent rover in rovers)
            if(rover!=null && rover!=applicant && rover.Hangar==hangar && (rover.OnRamp || rover.Docking)) return false;
        return true;
    }

    void BuildRampRoute(Agent agent,BuildingInstance hangar,int index,bool entering)
    {
        agent.OnRamp=true;

        for(int i=0;i<=20;i++)
        {
            float z=Mathf.Lerp(RoverRampExit,6.1f,(entering?i:20-i)/20f);
            Vector3 point=Local(hangar,0f,.62f,z);
            if(z>10.9f)
            {
                point=Grounded(point);
                if(Physics.Raycast(point+Vector3.up*3f,Vector3.down,out RaycastHit hit,3.1f) &&
                    hit.collider.GetComponentInParent<CargoHangarRamp>()!=null) point.y=hit.point.y;
            }
            agent.Route.Enqueue(point);
        }
        if(!entering)
            agent.Route.Enqueue(Grounded(Local(hangar,index==0?-9.5f:9.5f,0f,RoverRampExit+4f)));
    }

    bool FindStep(Agent agent,Vector3 target,bool rover)
    {
        Vector3 start=agent.Model.Root.position;
        Vector3 direction=target-start;
        direction.y=0f;
        if(direction.sqrMagnitude<.3f) return false;
        var points=new List<Vector3>(20);
        Vector3 facing=PressureCorridor.Horizontal(agent.Model.Root.forward);
        float beforeDistance=direction.magnitude;
        int checks=0;
        foreach(float turn in AlternativeTurns)
        {
            Vector3 aim=Quaternion.Euler(0f,turn,0f)*Vector3.ClampMagnitude(direction,rover?15f:12f);
            int sections=Mathf.CeilToInt(aim.magnitude/(rover?.91f:.68f));
            points.Clear();
            Vector3 before=start;
            for(int i=1;i<=sections;i++)
            {
                if(++checks>108) return false;
                float t=i/(float)sections;
                Vector3 step=aim*t;
                if(rover && Vector3.Dot(facing,aim)>0f)
                    step+=facing*(Mathf.Sin(t*Mathf.PI)*Mathf.Min(2.8f,aim.magnitude*.27f));
                Vector3 next=Grounded(start+step);
                Vector3 axis=PressureCorridor.Horizontal(next-before);
                if(Mathf.Abs(next.y-before.y)>(rover?.34f:.40f) || !IsOutsideClear(next,axis,rover,null) ||
                    !CanAdvance(agent,before,next,rover,true)) break;
                points.Add(next);
                before=next;
            }
            if(points.Count<3 && points.Count!=sections) continue;
            if(turn!=0f && Vector3.Distance(before,target)>beforeDistance+2.2f) continue;
            foreach(Vector3 point in points) agent.Route.Enqueue(point);
            return true;
        }
        return false;
    }

    bool IsOutsideClear(Vector3 point,Vector3 axis,bool rover,BuildingInstance rampOwner)
    {
        Vector3 relative=point-terrain.transform.position;
        Vector3 size=terrain.terrainData.size;
        float border=rover?6f:1f;
        if(relative.x<border || relative.z<border || relative.x>size.x-border || relative.z>size.z-border)
            return false;
        // A short horizontal capsule represents a rover's wheels and stowed arm.
        // The Terrain itself is handled by the height/grade sampler.
        Vector3 center=point+Vector3.up*(rover?.94f:.56f);
        float radius=rover?RoverClearance:.44f;
        Vector3 extent=rover?axis*1.97f:Vector3.up*.40f;
        int count=Physics.OverlapCapsuleNonAlloc(center-extent,center+extent,radius,movementHits,
            Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
        for(int i=0;i<count;i++)
        {
            Collider hit=movementHits[i];
            if(hit==null || hit is TerrainCollider) continue;
            if(rampOwner!=null && hit.transform.IsChildOf(rampOwner.transform)) continue;
            return false;
        }
        return count<movementHits.Length;
    }

    bool CanAdvance(Agent agent,Vector3 from,Vector3 to,bool rover,bool outside)
    {
        if(!outside) return ClearGroup(people,false);
        return ClearGroup(rovers,true) && ClearGroup(suited,false);

        bool ClearGroup(Agent[] group,bool vehicles)
        {
            foreach(Agent other in group)
            {
                if(other==null || other==agent || !other.Model.Root.gameObject.activeSelf) continue;
                Vector3 offset=other.Model.Root.position-from;
                if(Mathf.Abs(offset.y)>2.20f) continue;
                float margin=(rover?RoverClearance:PersonClearance)+(vehicles?RoverClearance:PersonClearance);
                float reach=margin+(rover?1.97f:0f)+(vehicles?1.97f:0f)+(to-from).magnitude;
                if(offset.x*offset.x+offset.z*offset.z>reach*reach) continue;
                Vector3 heading=PressureCorridor.Horizontal(to-from);
                if(heading==Vector3.zero) heading=PressureCorridor.Horizontal(agent.Model.Root.forward);
                Vector3 lead=rover?heading*1.97f:Vector3.zero;
                Vector3 otherLead=vehicles?PressureCorridor.Horizontal(other.Model.Root.forward)*1.97f:Vector3.zero;
                Vector3 otherAt=other.Model.Root.position;
                float initial=Apart(from-lead,from+lead,otherAt-otherLead,otherAt+otherLead);
                float final=Apart(to-lead,to+lead,otherAt-otherLead,otherAt+otherLead);
                float centerSweep=PressureCorridor.DistanceToSegment(otherAt,from,to);
                if(Mathf.Min(final,centerSweep)<margin &&
                    (initial>=margin || final<=initial+.0005f)) return false;
            }
            return true;
        }
    }

    static float Apart(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
    {
        if(PressureNetwork.Intersection(a,b,c,d,out _,out _)) return 0f;
        return Mathf.Min(PressureCorridor.DistanceToSegment(a,c,d),PressureCorridor.DistanceToSegment(b,c,d),
            PressureCorridor.DistanceToSegment(c,a,b),PressureCorridor.DistanceToSegment(d,a,b));
    }

    void Step(Agent agent,float speed,bool outside,bool rover)
    {
        Vector3 position=agent.Model.Root.position, target=agent.Route.Peek();
        Vector3 planar=Vector3.ProjectOnPlane(target-position,Vector3.up);
        if(planar.sqrMagnitude>.003f)
        {
            Quaternion facing=Quaternion.LookRotation(planar);
            float heading=Quaternion.Angle(agent.Model.Root.rotation,facing);
            float turn=Mathf.Sign(Vector3.Dot(agent.Model.Root.right,planar))*Mathf.Min(29f,heading);
            agent.Turn=Mathf.MoveTowards(agent.Turn,turn,Time.deltaTime*65f);
            agent.Model.Root.rotation=Quaternion.RotateTowards(agent.Model.Root.rotation,facing,Time.deltaTime*(rover?46f:165f));
            if(rover && heading>34f)
            {
                agent.Speed=Mathf.MoveTowards(agent.Speed,0f,Time.deltaTime*4.2f);
                Animate(agent,0f,true,true);
                return;
            }
        }
        if(rover)
        {
            float stopDistance=Vector3.Distance(position,target)+(agent.Route.Count-1)*.73f;
            float desired=Mathf.Min(speed,Mathf.Sqrt(2f*3.2f*stopDistance));
            agent.Speed=Mathf.MoveTowards(agent.Speed,desired,Time.deltaTime*2.75f);
            speed=agent.Speed;
        }
        Vector3 arrived=Vector3.MoveTowards(position,target,speed*Time.deltaTime);
        if(outside && !agent.OnRamp)
            arrived.y=Mathf.Max(arrived.y,Ground(arrived));
        if(!CanAdvance(agent,position,arrived,rover,outside))
        {
            agent.Speed=Mathf.MoveTowards(agent.Speed,0f,Time.deltaTime*4.2f);
            if(outside && !agent.OnRamp)
            {
                agent.Route.Clear();
                agent.Next=Time.time+.42f;
            }
            Animate(agent,0f,outside,rover);
            return;
        }
        agent.Model.Root.position=arrived;
        if((arrived-target).sqrMagnitude<.0016f) agent.Route.Dequeue();
        Animate(agent,Vector3.Distance(position,arrived),outside,rover);
    }

    static void Animate(Agent agent,float travel,bool outside,bool rover)
    {
        if(!rover)
            agent.Gait=Mathf.MoveTowards(agent.Gait,travel>.00001f?1f:0f,Time.deltaTime*5.2f);
        agent.Cycle=Mathf.Repeat(agent.Cycle+travel*(rover?50.6f:(outside?4.05f:5.7f)),rover?360f:2f*Mathf.PI);
        if(rover)
        {
            for(int i=0;i<8;i++)
            {
                agent.Model.Joints[i].localRotation=Quaternion.Euler(agent.Cycle,0,0);
                float steering=(i%4==3?1f:i%4==0?-.55f:.08f)*agent.Turn;
                agent.Model.TurnPivots[i].localRotation=Quaternion.Euler(0f,steering,0f);
            }
            float phase=agent.HasWork?Mathf.Clamp01((Time.time-agent.WorkTime)/6f):0f;
            float reach=agent.HasWork?Mathf.Sin(phase*Mathf.PI):0f;
            float lift=agent.HasWork?Mathf.Clamp01((phase-.51f)/.22f)*(1f-Mathf.Clamp01((phase-.83f)/.17f)):0f;
            float variant=agent.Task==0?1f:agent.Task==1?.54f:.76f;
            Pose(agent.Model.Secondary[0],Quaternion.Euler(0,(agent.Task==0?-15f:27f)*reach+31f*lift,0));
            Pose(agent.Model.Secondary[1],Quaternion.Euler(-52f+108f*reach*variant-36f*lift,0,0));
            Pose(agent.Model.Secondary[2],Quaternion.Euler(-46f+68f*reach-31f*lift,0,0));
            float opened=agent.HasWork?Mathf.Lerp(36f,1.8f,Mathf.Clamp01((phase-.37f)/.18f)):5f;
            Pose(agent.Model.JawLeft,Quaternion.Euler(0,-opened,0));
            Pose(agent.Model.JawRight,Quaternion.Euler(0,opened,0));
            Vector3 from=agent.Model.Root.InverseTransformPoint(agent.Model.HydraulicBase.position);
            Vector3 to=agent.Model.Root.InverseTransformPoint(agent.Model.HydraulicTip.position);
            Quaternion cylinderAxis=Quaternion.FromToRotation(Vector3.up,to-from);
            PlaceActuator(agent.Model.HydraulicCylinder,from,Vector3.Lerp(from,to,.68f),cylinderAxis);
            PlaceActuator(agent.Model.HydraulicRod,Vector3.Lerp(from,to,.53f),to,cylinderAxis);
            bool carrying=agent.HasWork && agent.Task==0 && phase>.51f && phase<.95f;
            if(agent.Sample!=null && agent.Sample.gameObject.activeSelf!=carrying)
                agent.Sample.gameObject.SetActive(carrying);
            return;
        }
        Vector3 offset=agent.Model.Steering.localPosition;
        offset.y=agent.Gait*(.5f-.5f*Mathf.Cos(2f*agent.Cycle))*(outside?.062f:.021f);
        agent.Model.Steering.localPosition=offset;
        for(int i=0;i<4;i++)
        {
            float stride=agent.Cycle+(i%2==0?0f:Mathf.PI);
            float swing=agent.Gait*Mathf.Sin(stride)*(i<2?1f:-.72f)*(outside?26f:33f);
            if(i>=2 && agent.HasWork && travel<=.00001f)
                swing=(agent.Task==0?-65f:agent.Task==1?-44f:-36f)+Mathf.Sin(Time.time*(agent.Task+2f)+i*.8f)*11f;
            Transform limb=agent.Model.Joints[i];
            Pose(limb,Quaternion.Euler(swing,0f,(i%2==0?-1f:1f)*3.5f));
            float bend=i<2 ? (travel>.00001f?Mathf.Max(0f,Mathf.Sin(stride-.47f))*38f*agent.Gait:agent.HasWork && outside && agent.Task==0?24f:4f) :
                (agent.HasWork?42f+Mathf.Sin(Time.time*3f+i)*12f:17f+Mathf.Abs(swing)*.42f);
            Pose(agent.Model.Secondary[i],Quaternion.Euler(i<2?bend:-bend,0,0));
        }
        Pose(agent.Model.Steering,Quaternion.Euler(agent.HasWork && outside && agent.Task==0?16f:0f,
            travel>.00001f?Mathf.Sin(agent.Cycle)*4.3f:0f,Mathf.Sin(agent.Cycle)*2.1f));
        Pose(agent.Model.Head,Quaternion.Euler(agent.HasWork?9f:0f,agent.HasWork?Mathf.Sin(Time.time*1.7f)*17f:0f,0f));

        static void PlaceActuator(Transform part,Vector3 a,Vector3 b,Quaternion axis)
        {
            part.localPosition=(a+b)*.5f;
            part.localRotation=axis;
            Vector3 scale=part.localScale;
            scale.y=Vector3.Distance(a,b)*.5f;
            part.localScale=scale;
        }

        static void Pose(Transform pivot,Quaternion target) =>
            pivot.localRotation=Quaternion.RotateTowards(pivot.localRotation,target,Time.deltaTime*190f);
    }

    Vector3 Grounded(Vector3 point) {point.y=Ground(point); return point;}
    float Ground(Vector3 point) => terrain.transform.position.y+terrain.SampleHeight(point);
    static Vector3 Local(BuildingInstance room,float x,float y,float z) => room.transform.TransformPoint(new Vector3(x,y,z));

    void TickRocket()
    {
        if(launchPad==null || !buildings.Contains(launchPad))
        {
            ClearRocket();
            launched.RemoveWhere(pad=>pad==null || !buildings.Contains(pad));
            launchPad=Find(BuildingKind.LandingPad);
            if(launchPad==null || !launched.Add(launchPad))
            {
                launchPad=null;
                return;
            }
            launchTime=Time.time;
        }
        float sinceConstruction = Time.time - launchTime;
        if (!TryFlight(sinceConstruction, out float height, out bool firing))
        {
            ClearRocket();
            return;
        }
        if(rocket.root==null) rocket=models.Rocket(transform);
        Vector3 deck=Local(launchPad,0,.52f,0);
        if(!rocket.root.gameObject.activeSelf) rocket.root.gameObject.SetActive(true);
        rocket.root.position=deck+Vector3.up*height;
        float airborne=Mathf.Clamp01(height/100f);
        rocket.root.rotation=launchPad.transform.rotation*Quaternion.Euler(airborne*2.8f,0f,-airborne*1.3f);
        foreach(Transform strut in rocket.legs) strut.localRotation=Quaternion.Euler(LandingGearAngle(height),0,0);
        rocket.exhaust.gameObject.SetActive(firing);
        if(firing)
        {
            rocket.exhaust.localScale=new Vector3(1f,1f+Mathf.Sin(Time.time*30f)*.12f,1f);
            rocket.glow.intensity=5.1f+Mathf.Sin(Time.time*23f)*.66f;
        }

    }

    internal static float LandingGearAngle(float height)
    {
        float phase=Mathf.Clamp01((height-3f)/35f);
        return 137f*phase*phase*(3f-2f*phase);
    }

    internal static bool TryFlight(float sinceConstruction, out float height, out bool firing)
    {
        height = 0f;
        firing = false;
        float elapsed = sinceConstruction - Countdown;
        if (elapsed < 0f || elapsed >= Descent + SurfaceStay + Ascent) return false;
        if (elapsed < Descent)
        {
            firing = true;
            height = 230f * Mathf.Pow(1f - elapsed / Descent, 2.55f);
        }
        else if (elapsed >= Descent + SurfaceStay)
        {
            firing = true;
            height = 280f * Mathf.Pow((elapsed - Descent - SurfaceStay) / Ascent, 2.32f);
        }
        return true;
    }

    void ClearRocket()
    {
        if(rocket.root==null || !rocket.root.gameObject.activeSelf) return;
        rocket.root.gameObject.SetActive(false);

    }
}
