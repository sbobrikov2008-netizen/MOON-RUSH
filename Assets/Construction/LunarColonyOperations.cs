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
        public bool HasWork, Docking, OnRamp;
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
    (Transform root, Transform exhaust, Light glow, ParticleSystem dust) rocket;
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
            for(int i=0;i<2;i++)
            {
                TickSurface(suited,i,anchor,false);
                TickSurface(rovers,i,anchor,true);
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
        person.Model.Root.position=Local(home,index==0?2.5f:3.25f,1.47f,7.72f);
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
        Animate(agent,0f,false,false);
        if(Time.time<agent.Next) return;
        agent.HasWork=false;
        var path=FindWorkRoute(agent.Room,agent.Trips++%2==0?BuildingKind.ScienceLab:BuildingKind.Greenhouse);
        if(path.Count==0 && agent.Room==home) path=FindWorkRoute(agent.Room,BuildingKind.DockingNode);
        if(path.Count==0 && agent.Room!=home) path=FindWorkRoute(agent.Room,BuildingKind.Habitat);
        if(path.Count==0)
        {
            if(agent.Room!=home || !PatrolConnectedCorridor(agent,index))
                agent.Route.Enqueue(Local(agent.Room,index==0?2.5f:3.25f,1.47f,agent.Trips%2==0?6.2f:8.1f));
            agent.Next=Time.time+3.5f;
            return;
        }
        foreach(var leg in path)
        {
            Transform start=leg.forward?leg.corridor.StartPort:leg.corridor.EndPort;
            Transform end=leg.forward?leg.corridor.EndPort:leg.corridor.StartPort;
            BuildingInstance target=leg.forward?leg.corridor.Second:leg.corridor.First;
            AddInterior(agent,agent.Room,start,true,index);
            var line=PressureCorridor.SmoothCenterline(leg.corridor.Route,out _);
            for(int i=0;i<line.Count;i++)
            {
                int j=leg.forward?i:line.Count-i-1;
                agent.Route.Enqueue(line[j].point+Vector3.up*PressureCorridor.Floor);
            }
            AddInterior(agent,target,end,false,index);
            agent.Room=target;
        }
        agent.HasWork=agent.Room.Kind==BuildingKind.ScienceLab || agent.Room.Kind==BuildingKind.Greenhouse;
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
            var line=PressureCorridor.SmoothCenterline(corridor.Route,out _);
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
            Vector3 At(int i) => line[forward?i:line.Count-1-i].point+Vector3.up*PressureCorridor.Floor;
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
            case BuildingKind.Greenhouse: return room.transform.InverseTransformPoint(port.position).z>0f;
            default: return false;
        }
    }

    void AddInterior(Agent agent,BuildingInstance room,Transform port,bool leaving,int index)
    {
        var points=new List<Vector3>();
        switch(room.Kind)
        {
            case BuildingKind.Habitat:
                points.Add(Local(room,index==0?2.5f:3.25f,1.47f,7.72f));
                points.Add(Local(room,0f,1.47f,9.1f));
                points.Add(Local(room,0f,.83f,11.3f));
                break;
            case BuildingKind.ScienceLab:
                points.Add(Local(room,index==0?-3.08f:3.08f,.65f,0f));
                points.Add(Local(room,0f,.65f,0f));
                break;
            case BuildingKind.Greenhouse:
                points.Add(Local(room,index==0?-.75f:.75f,.73f,4.3f));
                points.Add(Local(room,0f,.73f,8.7f));
                break;
            default:
                points.Add(Local(room,0f,.62f,0f));
                break;
        }
        if(!leaving) points.Reverse();
        foreach(Vector3 point in points) agent.Route.Enqueue(point);
    }

    void TickSurface(Agent[] group,int index,BuildingInstance anchor,bool rover)
    {
        Agent agent=group[index];
        if(agent==null)
        {
            BuildingInstance airlock=rover?null:Find(BuildingKind.AirlockCapsule);
            Vector3 spawn;
            if(airlock!=null && TryExitAirlock(airlock,index,out spawn))
            {
                agent=group[index]=new Agent{Model=models.Person(transform,true,index),Airlock=airlock};
                agent.Model.Root.position=spawn;
                for(int i=1;i<=8;i++) agent.Route.Enqueue(AirlockStep(airlock,index,i));
                agent.OnRamp=true;
                return;
            }
            for(int i=0;i<12;i++)
            {
                Quaternion turn=Quaternion.Euler(0f,index*91f+i*30f+(rover?45f:0f),0);
                Vector3 candidate=Grounded(anchor.transform.position+turn*Vector3.forward*(anchor.Definition.footprintRadius+7f));
                if(!IsOutsideClear(candidate,rover,group,index)) continue;
                agent=group[index]=new Agent{Model=rover?models.Rover(transform):models.Person(transform,true,index)};
                agent.Model.Root.position=candidate;
                break;
            }
            if(agent==null) return;
        }
        if(agent.OnRamp && ((rover && agent.Hangar==null) || (!rover && agent.Airlock==null)))
        {
            agent.OnRamp=false;
            agent.Docking=false;
            agent.Route.Clear();
            agent.Model.Root.position=Grounded(agent.Model.Root.position);
        }
        if(agent.Route.Count>0)
        {
            if (!agent.OnRamp && Time.time >= agent.NextClearanceCheck)
            {
                agent.NextClearanceCheck = Time.time + .12f;
                if (!IsOutsideClear(agent.Route.Peek(),rover,group,index))
                {
                    agent.Route.Clear();
                    agent.Next=Time.time+.65f+index*.3f;
                    return;
                }
            }
            Step(agent,rover?3.3f:1.18f,true,rover);
            if(agent.Route.Count==0)
            {
                agent.OnRamp=false;
                agent.Next=Time.time+(agent.Docking?4f:3.5f);
                agent.HasWork=!agent.Docking;
                if(!rover && agent.Trips>0 && agent.Trips%3==0) collect();
            }
            return;
        }
        Animate(agent,0f,true,rover);
        if(Time.time<agent.Next) return;
        agent.HasWork=false;
        agent.Next=Time.time+1.4f+index*.35f;
        if(agent.Docking && agent.Hangar!=null)
        {
            agent.Docking=false;
            BuildRampRoute(agent,agent.Hangar,index,false);
            return;
        }
        if(!rover && agent.Trips>=2)
        {
            BuildingInstance airlock=Find(BuildingKind.AirlockCapsule);
            if(airlock!=null && airlock!=agent.Airlock)
            {
                Vector3 entrance=Grounded(Local(airlock,index==0?-.6f:.6f,0f,8.5f));
                if(Vector3.Distance(agent.Model.Root.position,entrance)<.9f && TryExitAirlock(airlock,index,out Vector3 inside))
                {
                    agent.Airlock=airlock;
                    agent.OnRamp=true;
                    for(int i=8;i>=0;i--) agent.Route.Enqueue(AirlockStep(airlock,index,i));
                    agent.Route.Enqueue(inside);
                    for(int i=1;i<=8;i++) agent.Route.Enqueue(AirlockStep(airlock,index,i));
                    agent.Route.Enqueue(entrance);
                    closeAirlockAt=Time.time+15f;
                    return;
                }
                if(FindStep(agent,entrance,false,group,index)) return;
            }
        }
        agent.Hangar=rover?Find(BuildingKind.CargoHangar):null;
        if(agent.Hangar!=null && agent.Trips%4>=2)
        {
            Vector3 gate=Grounded(Local(agent.Hangar,index==0?-2f:2f,0f,24.4f));
            if(Vector3.Distance(agent.Model.Root.position,gate)<1.1f)
            {
                agent.Docking=true;
                BuildRampRoute(agent,agent.Hangar,index,true);
                agent.Trips++;
                return;
            }
            if(FindStep(agent,gate,rover,group,index))
            {
                agent.Trips=Mathf.Max(agent.Trips,2);
                return;
            }
        }
        for(int i=0;i<7;i++)
        {
            float angle=(agent.Trips*47f+i*52f+index*103f)%360f;
            Vector3 target=Grounded(agent.Model.Root.position+Quaternion.Euler(0,angle,0)*Vector3.forward*(rover?13f:7f));
            if(Vector3.Distance(target,anchor.transform.position)>145f) target=Grounded(Vector3.Lerp(target,anchor.transform.position,.18f));
            if(!FindStep(agent,target,rover,group,index)) continue;
            agent.Trips++;
            return;
        }
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

    void BuildRampRoute(Agent agent,BuildingInstance hangar,int index,bool entering)
    {
        agent.OnRamp=true;
        float lane=index==0?-2f:2f;
        for(int i=0;i<=20;i++)
        {
            float z=Mathf.Lerp(24.4f,6.1f,(entering?i:20-i)/20f);
            Vector3 point=Local(hangar,lane,.62f,z);
            if(z>10.9f)
            {
                point=Grounded(point);
                if(Physics.Raycast(point+Vector3.up*3f,Vector3.down,out RaycastHit hit,3.1f) &&
                    hit.collider.GetComponentInParent<CargoHangarRamp>()!=null) point.y=hit.point.y;
            }
            agent.Route.Enqueue(point);
        }
    }

    bool FindStep(Agent agent,Vector3 target,bool rover,Agent[] group,int index)
    {
        Vector3 start=agent.Model.Root.position;
        Vector3 direction=target-start;
        direction.y=0f;
        if(direction.sqrMagnitude<.3f) return false;
        foreach(float turn in new[]{0f,-32f,32f,-65f,65f,-100f,100f})
        {
            Vector3 aim=Quaternion.Euler(0f,turn,0f)*Vector3.ClampMagnitude(direction,13f);
            int sections=Mathf.CeilToInt(aim.magnitude/(rover?.85f:.62f));
            var points=new List<Vector3>();
            Vector3 before=start;
            for(int i=1;i<=sections;i++)
            {
                Vector3 next=Grounded(start+aim*(i/(float)sections));
                if(Mathf.Abs(next.y-before.y)>(rover?.29f:.37f) || !IsOutsideClear(next,rover,group,index)) break;
                points.Add(next);
                before=next;
            }
            if(points.Count!=sections) continue;
            foreach(Vector3 point in points) agent.Route.Enqueue(point);
            return true;
        }
        return false;
    }

    bool IsOutsideClear(Vector3 point,bool rover,Agent[] group,int index)
    {
        Vector3 relative=point-terrain.transform.position;
        Vector3 size=terrain.terrainData.size;
        if(relative.x<3f || relative.z<3f || relative.x>size.x-3f || relative.z>size.z-3f) return false;
        float radius=rover?1.57f:.39f;
        if(Physics.CheckSphere(point+Vector3.up*(radius+.13f),radius,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            return false;
        return ClearAgents(suited,1f) && ClearAgents(rovers,2.1f);

        bool ClearAgents(Agent[] others,float clearance)
        {
            for(int i=0;i<others.Length;i++)
            {
                if(others==group && i==index || others[i]==null) continue;
                if((others[i].Model.Root.position-point).sqrMagnitude<(radius+clearance)*(radius+clearance)) return false;
            }
            return true;
        }
    }

    void Step(Agent agent,float speed,bool outside,bool rover)
    {
        Vector3 position=agent.Model.Root.position, target=agent.Route.Peek();
        Vector3 planar=Vector3.ProjectOnPlane(target-position,Vector3.up);
        if(planar.sqrMagnitude>.003f)
        {
            Quaternion facing=Quaternion.LookRotation(planar);
            agent.Model.Root.rotation=Quaternion.RotateTowards(agent.Model.Root.rotation,facing,Time.deltaTime*(rover?95f:250f));
            if(rover && Quaternion.Angle(agent.Model.Root.rotation,facing)>34f) return;
        }
        Vector3 arrived=Vector3.MoveTowards(position,target,speed*Time.deltaTime);
        if(outside && !agent.OnRamp)
            arrived.y=Mathf.Max(arrived.y,Ground(arrived));
        agent.Model.Root.position=arrived;
        if((arrived-target).sqrMagnitude<.0016f) agent.Route.Dequeue();
        Animate(agent,Vector3.Distance(position,arrived),outside,rover);
    }

    static void Animate(Agent agent,float travel,bool outside,bool rover)
    {
        agent.Cycle+=travel*(rover?136.4f:(outside?4.7f:6.7f));
        if(rover)
        {
            foreach(Transform wheel in agent.Model.Joints) wheel.localRotation=Quaternion.Euler(agent.Cycle,0,0);
            agent.Model.Steering.Rotate(Vector3.up,agent.HasWork?230f*Time.deltaTime:0f,Space.Self);
            return;
        }
        Vector3 offset=agent.Model.Steering.localPosition;
        offset.y=travel>.00001f ? Mathf.Abs(Mathf.Sin(agent.Cycle))*(outside?.105f:.025f) : 0f;
        agent.Model.Steering.localPosition=offset;
        for(int i=0;i<4;i++)
        {
            float swing=travel>.00001f?Mathf.Sin(agent.Cycle)*((i==0 || i==3)?1f:-1f)*(outside?22f:31f):0f;
            if(i>=2 && agent.HasWork && travel<=.00001f) swing=-47f+Mathf.Sin(Time.time*5f+i)*9f;
            Transform limb=agent.Model.Joints[i];
            limb.localRotation=Quaternion.RotateTowards(limb.localRotation,Quaternion.Euler(swing,0f,0f),Time.deltaTime*290f);
        }
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
        rocket.root.position=deck+Vector3.up*height;
        rocket.exhaust.gameObject.SetActive(firing);
        if(firing)
        {
            rocket.exhaust.localScale=new Vector3(1f,1f+Mathf.Sin(Time.time*30f)*.12f,1f);
            rocket.glow.intensity=7f+Mathf.Sin(Time.time*23f)*1.3f;
        }
        rocket.dust.transform.position=deck+Vector3.up*.21f;
        if(firing && height<12f)
        {
            if(!rocket.dust.isPlaying) rocket.dust.Play();
        }
        else if(rocket.dust.isEmitting) rocket.dust.Stop(true,ParticleSystemStopBehavior.StopEmitting);
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
        if(rocket.root!=null) Destroy(rocket.root.gameObject);
        if(rocket.dust!=null) Destroy(rocket.dust.gameObject);
        rocket=default;
    }
}
