using UnityEngine;

internal sealed class IndustrialPipeVisual : MonoBehaviour
{
    Material[] materials, activeMaterials;
    Mesh mesh, flightMesh;
    readonly Mesh[] cargoMeshes=new Mesh[4];
    Material rockMaterial;
    ConveyorTransit transit;
    float lastCargoTick;
    Transform geometry;
    Vector3[] previous;
    float[] distances;
    Transform[] rocks, flights;
    MeshFilter[] rockFilters;
    float travel;
    bool conveyor, running;

    internal void Initialize(Material body, Material collar, Material stripe, Material glass = null, Material ore = null, Material warningLight = null)
    {
        conveyor=glass!=null;
        materials=conveyor?new[]{body,collar,stripe,glass,collar}:new[]{body,collar,stripe};
        activeMaterials=conveyor?new[]{body,collar,stripe,glass,warningLight}:materials;
        if(!conveyor) return;
        rocks=new Transform[ConveyorTransit.RockLimit];
        rockFilters=new MeshFilter[rocks.Length];
        rockMaterial=ore;
        flights=new Transform[24];
        var cleat=new ExpeditionMesh(1);
        cleat.Box(0,Vector3.zero,new Vector3(1.19f,.025f,.09f),Quaternion.identity);
        flightMesh=cleat.Build("Pooled conveyor cleat");
        for(int i=0;i<flights.Length;i++)
            flights[i]=PoolPart("Belt cleat",flightMesh,collar);
        for(int type=0;type<cargoMeshes.Length;type++)
        {
            var rock=new ExpeditionMesh(1);
            const int sides=7;
            var bottom=new Vector3[sides];
            var shoulders=new Vector3[sides];
            for(int i=0;i<sides;i++)
            {
                float angle=i*Mathf.PI*2f/sides;
                float width=.22f+.045f*((i*3+type*7)%4);
                bottom[i]=new Vector3(Mathf.Cos(angle)*width,.006f,Mathf.Sin(angle)*width);
                shoulders[i]=bottom[i]*(.67f+.08f*((i+type)%3))+Vector3.up*(.12f+.035f*((i*2+type)%5));
            }
            var top=new Vector3((type-1.5f)*.07f,.29f+type*.035f,-.07f);
            for(int i=0;i<sides;i++)
            {
                int j=(i+1)%sides;
                rock.Quad(0,bottom[i],shoulders[i],shoulders[j],bottom[j]);
                rock.Face(0,shoulders[i],top,shoulders[j]);
            }
            cargoMeshes[type]=rock.Build("Basalt cargo shape "+type);
        }
        enabled=false;
    }

    Transform PoolPart(string name,Mesh data,Material material)
    {
        var item=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
        item.transform.SetParent(transform,false);
        item.GetComponent<MeshFilter>().sharedMesh=data;
        item.GetComponent<MeshRenderer>().sharedMaterial=material;
        item.SetActive(false);
        return item.transform;
    }

    internal void BindCargo(ConveyorTransit cargo) => transit=cargo;

    internal void SyncCargo()
    {
        if(!conveyor || transit==null) return;
        lastCargoTick=Time.time;
        if(running!=transit.Moving)
        {
            running=transit.Moving;
            enabled=running;
            if(geometry!=null) geometry.GetComponent<MeshRenderer>().sharedMaterials=running?activeMaterials:materials;
        }
        PlaceCargo();
    }

    void Update() => PlaceCargo();

    void PlaceCargo()
    {
        if(transit==null || distances==null || distances.Length<2) return;
        float length=distances[distances.Length-1];
        if(length<.01f) return;
        double progress=transit.Position+(running?Mathf.Clamp(Time.time-lastCargoTick,0f,.4f)*ConveyorTransit.Speed:0f);
        travel=(float)(progress%length);
        int segment=1;
        for(int i=0;i<flights.Length;i++)
        {
            float at=Mathf.Repeat(travel+i*length/flights.Length,length);
            Transform cleat=flights[i];
            if(!cleat.gameObject.activeSelf) cleat.gameObject.SetActive(true);
            Move(cleat,at,0f,.099f,0f);
        }
        int visible=0;
        segment=1;
        foreach(var load in transit.Loads)
        {
            float along=(float)((progress-load.Start)/transit.Length);
            if(along<0f || along>=1f || visible>=rocks.Length) continue;
            int type=load.Id%cargoMeshes.Length;
            Transform rock=rocks[visible];
            if(rock==null)
            {
                rock=rocks[visible]=PoolPart("Belt cargo",cargoMeshes[type],rockMaterial);
                rockFilters[visible]=rock.GetComponent<MeshFilter>();
            }
            var filter=rockFilters[visible];
            if(filter.sharedMesh!=cargoMeshes[type]) filter.sharedMesh=cargoMeshes[type];
            rock.localScale=Vector3.one*(.82f+.085f*((load.Id*7)%6));
            if(!rock.gameObject.activeSelf) rock.gameObject.SetActive(true);
            Move(rock,along*length,(load.Id%3-1)*.14f,.117f,(load.Id*53)%360);
            visible++;
        }
        for(int i=visible;i<rocks.Length && rocks[i]!=null;i++)
            if(rocks[i].gameObject.activeSelf) rocks[i].gameObject.SetActive(false);

        void Move(Transform item,float at,float offset,float lift,float yaw)
        {
            if(at<distances[segment-1]) segment=1;
            while(segment<distances.Length-1 && at>distances[segment]) segment++;
            Vector3 a=previous[segment-1],b=previous[segment],axis=b-a;
            if(axis.sqrMagnitude<.000001f) return;
            Vector3 position=Vector3.Lerp(a,b,(at-distances[segment-1])/
                Mathf.Max(.001f,distances[segment]-distances[segment-1]));
            Vector3 right=Vector3.Cross(Vector3.up,axis).normalized;
            item.SetPositionAndRotation(position+Vector3.up*lift+right*offset,
                Quaternion.LookRotation(axis)*Quaternion.Euler(0,yaw,0));
        }
    }

    internal void SetRoute(Vector3[] route)
    {
        if(route==null || route.Length<2 || materials==null) return;
        if (previous != null && previous.Length == route.Length)
        {
            int sample=0;
            while(sample<route.Length && previous[sample]==route[sample]) sample++;
            if(sample==route.Length) return;
        }
        previous=route;
        if(geometry!=null) { geometry.gameObject.SetActive(false); Destroy(geometry.gameObject); Destroy(mesh); }
        geometry=new GameObject(conveyor?"Conveyor housing":"Industrial pipe",typeof(MeshFilter),typeof(MeshRenderer)).transform;
        geometry.SetParent(transform,false);
        var shape=new ExpeditionMesh(materials.Length);
        distances=new float[route.Length];
        float fitting=.2f;
        for(int i=1;i<route.Length;i++)
        {
            Vector3 a=route[i-1],b=route[i];
            float length=Vector3.Distance(a,b);
            distances[i]=distances[i-1]+length;
            if(length<.001f) continue;
            Vector3 axis=(b-a)/length,right=Vector3.Cross(Vector3.up,axis).normalized;
            Quaternion rotation=Quaternion.LookRotation(axis);
            if(conveyor)
            {
                // Continuous housing follows averaged tangents through every route bend.
                Vector3 r1=Right(i-1),r2=Right(i);
                Vector3 P(Vector3 p,Vector3 r,float x,float y)=>p+r*x+Vector3.up*y;
                shape.Quad(0,P(a,r1,-.62f,.08f),P(b,r2,-.62f,.08f),P(b,r2,.62f,.08f),P(a,r1,.62f,.08f));
                shape.Quad(3,P(a,r1,-.87f,.04f),P(b,r2,-.87f,.04f),P(b,r2,-.70f,.81f),P(a,r1,-.70f,.81f));
                shape.Quad(3,P(a,r1,.70f,.81f),P(b,r2,.70f,.81f),P(b,r2,.87f,.04f),P(a,r1,.87f,.04f));
                shape.Quad(3,P(a,r1,-.70f,.81f),P(b,r2,-.70f,.81f),P(b,r2,.70f,.81f),P(a,r1,.70f,.81f));
                foreach(float side in new[]{-.78f,.78f})
                    shape.Box(1,(a+b)*.5f+right*side,new Vector3(.12f,.23f,length+.07f),rotation);
                shape.Tube(1,(a+b)*.5f-right*.68f,(a+b)*.5f+right*.68f,.075f,.075f,6);
            }
            else shape.Tube(0,a-axis*.04f,b+axis*.04f,.33f,.33f);
            while(fitting<length)
            {
                Vector3 p=a+axis*fitting;
                if(conveyor)
                {
                    foreach(float side in new[]{-.78f,.78f})
                    {
                        shape.Box(1,p+right*side+Vector3.up*.28f,new Vector3(.12f,1.02f,.15f),rotation);
                        shape.Box(2,p+right*side+Vector3.up*.23f,new Vector3(.14f,.12f,.17f),rotation);
                    }
                    shape.Box(1,p+Vector3.up*.82f,new Vector3(1.51f,.12f,.15f),rotation);
                    shape.Box(4,p+Vector3.up*.74f,new Vector3(.61f,.023f,.081f),rotation);
                    shape.Box(1,p+Vector3.up*-.39f,new Vector3(1.64f,.66f,.19f),rotation);
                    shape.Box(2,p+right*.89f,new Vector3(.23f,.28f,.37f),rotation);
                }
                else
                {
                    shape.Tube(1,p-axis*.12f,p+axis*.12f,.42f,.42f);
                    shape.Tube(2,p+axis*.075f,p+axis*.15f,.429f,.429f);
                    shape.Box(1,p+Vector3.up*-.38f,new Vector3(.64f,.19f,.47f),rotation);
                    if(i%3==0)
                    {
                        shape.Tube(1,p+Vector3.up*.22f,p+Vector3.up*.62f,.12f,.12f);
                        shape.Tube(2,p+Vector3.up*.61f,p+Vector3.up*.68f,.24f,.24f);
                    }
                }
                fitting+=conveyor?2.2f:3.15f;
            }
            fitting-=length;
        }
        for(int start=0;start+1<route.Length;)
        {
            int end=start+1;
            Vector3 a=route[start], axis=(route[end]-a).normalized;
            while(end+1<route.Length && Vector3.Distance(a,route[end+1])<7.5f &&
                Vector3.Cross(axis,route[end+1]-a).sqrMagnitude<.024f) end++;
            AddPickVolume(a,route[end]);
            start=end;
        }
        mesh=shape.Build("Batched industrial route");
        geometry.GetComponent<MeshFilter>().sharedMesh=mesh;
        geometry.GetComponent<MeshRenderer>().sharedMaterials=running?activeMaterials:materials;
        if(conveyor) PlaceCargo();

        Vector3 Right(int i) => Vector3.Cross(Vector3.up,
            route[Mathf.Min(route.Length-1,i+1)]-route[Mathf.Max(0,i-1)]).normalized;
    }

    void AddPickVolume(Vector3 a,Vector3 b)
    {
        Vector3 delta=b-a;
        if(delta.sqrMagnitude<.00001f) return;
        // Ignore Raycast keeps the proxy out of movement and construction collision checks.
        var proxy=new GameObject("Inspect industrial route",typeof(BoxCollider)){layer=2};
        proxy.transform.SetParent(geometry,false);
        proxy.transform.SetPositionAndRotation((a+b)*.5f+Vector3.up*(conveyor?.35f:0f),
            Quaternion.LookRotation(delta));
        var hit=proxy.GetComponent<BoxCollider>();
        hit.size=new Vector3(conveyor?1.73f:.67f,conveyor?.92f:.67f,delta.magnitude+.025f);
        hit.isTrigger=true;
    }

    void OnDestroy()
    {
        Destroy(mesh);
        foreach(Mesh rock in cargoMeshes) Destroy(rock);
        Destroy(flightMesh);
    }
}
