using UnityEngine;

internal sealed class IndustrialPipeVisual : MonoBehaviour
{
    Material[] materials, activeMaterials;
    Mesh mesh, cargoMesh, flightMesh;
    Transform geometry;
    Vector3[] previous;
    float[] distances;
    Transform[] rocks, flights;
    float travel;
    bool conveyor, running;

    internal void Initialize(Material body, Material collar, Material stripe, Material glass = null, Material ore = null, Material warningLight = null)
    {
        conveyor=glass!=null;
        materials=conveyor?new[]{body,collar,stripe,glass,collar}:new[]{body,collar,stripe};
        activeMaterials=conveyor?new[]{body,collar,stripe,glass,warningLight}:materials;
        if(!conveyor) return;
        rocks=new Transform[10];
        flights=new Transform[24];
        var cleat=new ExpeditionMesh(1);
        cleat.Box(0,Vector3.zero,new Vector3(1.19f,.025f,.09f),Quaternion.identity);
        flightMesh=cleat.Build("Pooled conveyor cleat");
        for(int i=0;i<flights.Length;i++)
            flights[i]=PoolPart("Belt cleat",flightMesh,collar);
        var chunk=new ExpeditionMesh(1);
        var shape=new[]{new Vector3(-.24f,0,-.17f),new Vector3(.22f,0,-.21f),new Vector3(.29f,0,.16f),
            new Vector3(-.22f,0,.22f)};
        Vector3 ridge=new Vector3(.08f,.24f,-.04f);
        for(int i=0;i<shape.Length;i++) chunk.Face(0,shape[(i+1)%shape.Length],shape[i],ridge);
        cargoMesh=chunk.Build("Pooled basalt cargo");
        for(int i=0;i<rocks.Length;i++)
        {
            rocks[i]=PoolPart("Belt cargo",cargoMesh,ore);
            rocks[i].localScale=Vector3.one*(.68f+.13f*(i%4));
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

    internal void SetMoving(bool active)
    {
        if(!conveyor || running==active) return;
        running=active;
        enabled=active;
        if(geometry!=null) geometry.GetComponent<MeshRenderer>().sharedMaterials=active?activeMaterials:materials;
    }

    void Update()
    {
        if(distances==null || distances.Length<2) return;
        travel+=Time.deltaTime*2.4f;
        PlaceCargo();
    }

    void PlaceCargo()
    {
        float length=distances[distances.Length-1];
        float loop=Mathf.Max(length,rocks.Length*1.6f);
        int segment=1;
        for(int i=0;i<flights.Length;i++)
        {
            float at=Mathf.Repeat(travel+i*loop/flights.Length,loop);
            Transform cleat=flights[i];
            bool visible=at<length-.15f && at>.15f;
            if(cleat.gameObject.activeSelf!=visible) cleat.gameObject.SetActive(visible);
            if(visible) MoveCleat(cleat,at);
        }
        for(int i=0;i<rocks.Length;i++)
        {
            float at=Mathf.Repeat(travel+i*loop/rocks.Length,loop);
            Transform rock=rocks[i];
            bool visible=at<length-.15f && at>.15f;
            if(rock.gameObject.activeSelf!=visible) rock.gameObject.SetActive(visible);
            if(!visible) continue;
            MoveCleat(rock,at);
            rock.position+=Vector3.up*.032f;
        }
        void MoveCleat(Transform item,float at)
        {
            if(at<distances[segment-1]) segment=1;
            while(segment<distances.Length-1 && at>distances[segment]) segment++;
            Vector3 a=previous[segment-1],b=previous[segment];
            item.SetPositionAndRotation(Vector3.Lerp(a,b,(at-distances[segment-1])/
                Mathf.Max(.001f,distances[segment]-distances[segment-1]))+Vector3.up*.098f,
                Quaternion.LookRotation(b-a));
        }
    }

    internal void SetRoute(Vector3[] route)
    {
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
        Destroy(cargoMesh);
        Destroy(flightMesh);
    }
}
