using System;
using UnityEngine;

internal sealed class LunarVehicleModels
{
    readonly Func<string,Color,Material> materials;
    internal LunarVehicleModels(Func<string,Color,Material> materials) => this.materials = materials;

    internal sealed class Model
    {
        internal Transform Root;
        internal Transform[] Joints;
        internal Transform Steering;
        internal Transform[] Secondary, TurnPivots;
        internal Transform Tool, JawLeft, JawRight, Head;
    }

    internal Model Person(Transform parent, bool suited, int index)
    {
        Material fabric = Mat(suited ? "Suit fabric" : "Crew uniform " + index,
            suited ? new Color(.83f,.82f,.75f) : index == 0 ? new Color(.20f,.49f,.63f) : new Color(.58f,.29f,.16f));
        Material boot = Mat("Personnel joints",new Color(.13f,.16f,.19f));
        Material trim = Mat("Suit orange",new Color(.88f,.37f,.095f));
        Material face = Mat("Crew face " + index,index == 0 ? new Color(.63f,.43f,.32f) : new Color(.42f,.28f,.22f));
        Transform root = Root(parent,suited ? "Астронавт" : "Житель базы");
        Transform body = Root(root,"Animated body");
        Part(body,PrimitiveType.Capsule,new Vector3(0,1.20f,0),new Vector3(.53f,.34f,.32f),fabric);
        Part(body,PrimitiveType.Cube,new Vector3(0,1.02f,0),new Vector3(.40f,.17f,.28f),boot);
        Transform head=Root(body,"Helmet and head");
        head.localPosition=new Vector3(0,1.67f,0);
        Part(head,PrimitiveType.Sphere,Vector3.zero,suited?new Vector3(.37f,.39f,.39f):new Vector3(.23f,.25f,.235f),suited?fabric:face);
        Part(head,PrimitiveType.Sphere,new Vector3(0,.016f,.118f),new Vector3(.31f,.23f,.19f),
            suited?Mat("Visor gold",new Color(.38f,.25f,.12f)):face).gameObject.SetActive(suited);
        if (!suited)
        {
            Part(head,PrimitiveType.Sphere,new Vector3(0,.081f,-.025f),new Vector3(.235f,.132f,.241f),boot);
            for(int side=-1;side<=1;side+=2)
                Part(head,PrimitiveType.Sphere,new Vector3(side*.047f,.018f,.107f),new Vector3(.028f,.017f,.014f),boot);
        }
        else
        {
            Part(head,PrimitiveType.Cylinder,new Vector3(0,-.13f,0),new Vector3(.305f,.045f,.306f),boot);
            Part(head,PrimitiveType.Cube,new Vector3(.17f,.031f,.01f),new Vector3(.083f,.10f,.13f),trim);
        }
        if (suited)
        {
            Part(body,PrimitiveType.Cube,new Vector3(0,1.26f,-.239f),new Vector3(.41f,.56f,.25f),fabric);
            Part(body,PrimitiveType.Cylinder,new Vector3(.23f,1.22f,-.246f),new Vector3(.15f,.235f,.15f),trim);
            Part(body,PrimitiveType.Cube,new Vector3(0,1.28f,.185f),new Vector3(.23f,.22f,.10f),boot);
            Part(body,PrimitiveType.Cube,new Vector3(0,1.33f,.242f),new Vector3(.15f,.05f,.016f),trim);
        }
        var joints = new Transform[4];
        var limbs = new Transform[4];
        for (int i=0;i<4;i++)
        {
            bool arm=i>=2;
            float side=i%2==0?-1f:1f;
            var joint=joints[i]=Root(body,arm?"Shoulder":"Hip");
            joint.localPosition=new Vector3(side*(arm?.29f:.12f),arm?1.41f:.97f,0);
            float length=arm?.274f:.417f;
            Part(joint,PrimitiveType.Capsule,new Vector3(0,-length*.48f,0),
                new Vector3(arm?.167f:.208f,length*.58f,arm?.158f:.205f),fabric);
            var bend=limbs[i]=Root(joint,arm?"Elbow":"Knee");
            bend.localPosition=Vector3.down*length;
            Part(bend,PrimitiveType.Sphere,Vector3.zero,Vector3.one*(arm?.145f:.163f),boot);
            Part(bend,PrimitiveType.Capsule,new Vector3(0,-length*.43f,.013f),
                new Vector3(arm?.146f:.178f,length*.55f,arm?.139f:.177f),fabric);
            Part(bend,PrimitiveType.Sphere,new Vector3(0,-length,arm?.012f:.066f),
                arm?new Vector3(.13f,.14f,.15f):new Vector3(.19f,.142f,.295f),arm&&!suited?face:boot);
            if (suited) Part(joint,PrimitiveType.Cube,new Vector3(0,-length*.78f,.086f),
                new Vector3(.13f,.078f,.063f),trim);
        }
        return new Model { Root=root,Joints=joints,Secondary=limbs,Steering=body,Head=head };
    }

    internal Model Rover(Transform parent)
    {
        var root=Root(parent,"Восьмиколёсный исследовательский луноход");
        root.localScale=Vector3.one*1.5f;
        Material chassis=Mat("Rover alloy",new Color(.69f,.72f,.73f));
        Material dark=Mat("Rover structure",new Color(.14f,.17f,.20f));
        Material tread=Mat("Rover tread",new Color(.075f,.09f,.102f));
        Material blue=Mat("Rover photovoltaic cells",new Color(.11f,.25f,.44f));
        Material orange=Mat("Suit orange",new Color(.88f,.37f,.095f));
        Part(root,PrimitiveType.Cube,new Vector3(0,.67f,0),new Vector3(2.19f,.39f,3.03f),dark);
        Part(root,PrimitiveType.Cube,new Vector3(0,.96f,.16f),new Vector3(1.87f,.52f,2.64f),chassis);
        Part(root,PrimitiveType.Cube,new Vector3(0,1.05f,1.49f),new Vector3(1.57f,.25f,.29f),orange);
        Part(root,PrimitiveType.Cylinder,new Vector3(-.33f,1.50f,.77f),new Vector3(.14f,.44f,.14f),chassis);
        Part(root,PrimitiveType.Cube,new Vector3(-.33f,1.99f,.84f),new Vector3(.52f,.24f,.31f),dark);
        Part(root,PrimitiveType.Sphere,new Vector3(-.33f,1.99f,1.00f),Vector3.one*.15f,blue);
        Part(root,PrimitiveType.Cylinder,new Vector3(.55f,1.58f,-.83f),new Vector3(.04f,.55f,.04f),chassis);
        Part(root,PrimitiveType.Sphere,new Vector3(.55f,2.16f,-.83f),Vector3.one*.15f,orange);
        for (int row=0;row<6;row++)
        for (int col=0;col<4;col++)
            Part(root,PrimitiveType.Cube,new Vector3((col-1.5f)*.39f,1.27f,-.83f+.23f*row),
                new Vector3(.374f,.073f,.216f),blue);
        Material light=Mat("Rover lamp ceramic",new Color(.72f,.88f,.96f));
        Material alloy=Mat("Rover brushed components",new Color(.42f,.46f,.50f));
        for(int side=-1;side<=1;side+=2)
        {
            Part(root,PrimitiveType.Cube,new Vector3(side*.62f,1.12f,1.61f),new Vector3(.37f,.16f,.16f),light);
            Part(root,PrimitiveType.Cube,new Vector3(side*.98f,1.08f,-.93f),new Vector3(.34f,.45f,.7f),orange);
            Part(root,PrimitiveType.Cylinder,new Vector3(side*.91f,1.27f,.39f),new Vector3(.17f,.44f,.17f),alloy,Quaternion.Euler(90,0,0));
        }
        var wheels=new Transform[8];
        var steering=new Transform[8];
        for(int i=0;i<wheels.Length;i++)
        {
            float side=i<4?-1f:1f, z=(i%4-1.5f)*.88f;
            var axle=steering[i]=Root(root,"Suspension and steering");
            axle.localPosition=new Vector3(side*1.16f,.45f,z);
            var joint=wheels[i]=Root(axle,"Wheel rotation");
            var wheel=new ExpeditionMesh(3);
            wheel.Tube(0,Vector3.left*.215f,Vector3.right*.215f,.429f,.429f,20);
            wheel.Tube(1,Vector3.right*side*.215f,Vector3.right*side*.246f,.318f,.282f,14);
            for(int segment=0;segment<20;segment++)
            {
                Vector3 a=Quaternion.Euler(segment*18f,0,0)*Vector3.up*.43f;
                Vector3 b=Quaternion.Euler((segment+1)*18f,0,0)*Vector3.up*.43f;
                Vector3 outer=Vector3.right*.215f;
                wheel.Face(0,outer,a+outer,b+outer);
                wheel.Face(0,-outer,b-outer,a-outer);
            }
            for(int lug=0;lug<12;lug++)
            {
                Quaternion angle=Quaternion.Euler(lug*30f,0,0);
                wheel.Box(0,angle*new Vector3(0,.435f,0),new Vector3(.44f,.048f,.10f),angle);
                if(lug%2==0) wheel.Box(2,angle*new Vector3(side*.237f,.16f,0),new Vector3(.035f,.26f,.064f),angle);
            }
            MeshPart(joint,wheel.Build("Rover wheel"),new[]{tread,chassis,alloy});
            Part(root,PrimitiveType.Cube,new Vector3(side*.91f,.59f,z),new Vector3(.65f,.095f,.13f),orange,
                Quaternion.Euler(0,0,side*-22f));
            Part(root,PrimitiveType.Cylinder,new Vector3(side*1.01f,.64f,z),new Vector3(.08f,.196f,.08f),alloy,
                Quaternion.Euler(0,0,side*23f));
        }
        Transform basePivot=Root(root,"Manipulator yaw");
        basePivot.localPosition=new Vector3(.21f,1.03f,1.45f);
        Part(basePivot,PrimitiveType.Cylinder,Vector3.zero,new Vector3(.34f,.17f,.34f),alloy);
        Transform shoulder=Root(basePivot,"Manipulator shoulder");
        shoulder.localPosition=Vector3.up*.09f;
        Part(shoulder,PrimitiveType.Cube,new Vector3(0,0,.32f),new Vector3(.19f,.15f,.66f),orange);
        Transform elbow=Root(shoulder,"Manipulator elbow");
        elbow.localPosition=new Vector3(0,0,.64f);
        Part(elbow,PrimitiveType.Cylinder,Vector3.zero,new Vector3(.22f,.13f,.22f),dark,Quaternion.Euler(0,0,90));
        Part(elbow,PrimitiveType.Cube,new Vector3(0,0,.258f),new Vector3(.11f,.11f,.51f),chassis);
        Transform claw=Root(elbow,"Gripper");
        claw.localPosition=new Vector3(0,0,.5f);
        Transform left=Finger(-1), right=Finger(1);
        return new Model{Root=root,Joints=wheels,TurnPivots=steering,Secondary=new[]{basePivot,shoulder,elbow},
            Tool=claw,JawLeft=left,JawRight=right};

        Transform Finger(float side)
        {
            Transform finger=Root(claw,"Claw jaw");
            finger.localPosition=new Vector3(side*.076f,0,0);
            Part(finger,PrimitiveType.Cube,new Vector3(side*.035f,0,.085f),new Vector3(.06f,.083f,.195f),dark);
            Part(finger,PrimitiveType.Cube,new Vector3(-side*.005f,0,.174f),new Vector3(.132f,.075f,.045f),alloy);
            return finger;
        }
    }

    internal (Transform root, Transform exhaust, Light glow, ParticleSystem dust, Transform[] legs) Rocket(Transform parent)
    {
        Transform root=Root(parent,"Многоразовый лунный шаттл");
        Material white=Mat("Rocket ceramic",new Color(.83f,.86f,.88f));
        Material graphite=Mat("Rocket heat shield",new Color(.095f,.11f,.134f));
        Material orange=Mat("Suit orange",new Color(.88f,.37f,.095f));
        Material glazing=Mat("Cockpit obsidian glass",new Color(.075f,.16f,.23f));
        glazing.SetFloat("_Smoothness",.78f);
        Material nickel=Mat("Engine nickel alloy",new Color(.52f,.55f,.58f));
        nickel.SetFloat("_Metallic",.62f);
        var hull=new ExpeditionMesh(5);
        const int sides=20;
        var rings=new (float y,float width,float depth,float offset)[]
        {
            (2.25f,2.20f,1.62f,-.12f),(3.22f,2.71f,1.86f,-.10f),(6.2f,2.66f,1.99f,-.12f),
            (12.3f,2.45f,1.98f,-.16f),(14.9f,2.18f,1.88f,-.19f),(16.58f,1.81f,1.51f,-.15f),
            (17.45f,1.34f,1.15f,-.06f),(18.92f,.77f,.73f,.03f),(19.77f,.21f,.22f,.17f),(19.86f,.012f,.012f,.22f)
        };
        for(int j=1;j<rings.Length;j++)
        for(int i=0;i<sides;i++)
        {
            bool cockpit=(j==6 || j==7) && (i is 2 or 3 or 4 or 5 or 6 or 7);
            int style=cockpit?3:i>=10 && i<=19 || j<=1?1:0;
            hull.Quad(style,R(j-1,i),R(j,i),R(j,i+1),R(j-1,i+1));
        }
        for(int pane=2;pane<=8;pane+=2)
        {
            Vector3 lower=R(5,pane)+Vector3.forward*.012f;
            Vector3 upper=R(7,pane)+Vector3.forward*.012f;
            hull.Tube(1,lower,upper,.039f,.029f,6);
        }
        for(int i=0;i<sides;i++)
            hull.Face(1,new Vector3(0,2.25f,-.12f),R(0,i),R(0,i+1));
        Vector3 R(int ring,int i)
        {
            var r=rings[ring];
            float angle=i*Mathf.PI*2f/sides;
            return new Vector3(r.width*Mathf.Cos(angle),r.y,r.offset+r.depth*Mathf.Sin(angle));
        }
        foreach(float side in new[]{-1f,1f})
        {
            Vector3 a=new Vector3(side*2.09f,11.5f,-.46f),b=new Vector3(side*7.4f,4.5f,-.38f);
            Vector3 c=new Vector3(side*7.87f,2.77f,-.41f),d=new Vector3(side*2.09f,3.18f,-.47f);
            Vector3 keel=new Vector3(side*3.4f,5.64f,-1.03f);
            if(side<0) { hull.Quad(0,a,b,c,d); hull.Quad(1,d,c,b,a); }
            else { hull.Quad(0,d,c,b,a); hull.Quad(1,a,b,c,d); }
            hull.Face(1,a,b,keel); hull.Face(1,b,c,keel); hull.Face(1,c,d,keel);
            hull.Tube(1,a,b,.17f,.11f);
            hull.Box(2,Vector3.Lerp(b,c,.73f),new Vector3(.20f,.49f,.19f),Quaternion.identity);
            hull.Box(1,new Vector3(side*1.55f,10.15f,1.33f),new Vector3(.043f,4.35f,.71f),Quaternion.Euler(0,side*-38f,0));
            for(int panel=0;panel<6;panel++)
                hull.Box(4,new Vector3(side*2.38f,4.73f+panel*.27f,-1.05f),new Vector3(.13f,.068f,.44f),Quaternion.identity);
        }
        // Bifurcated tail and deeply recessed bell throats.
        Vector3 t0=new Vector3(0,8.6f,-1.52f),t1=new Vector3(0,4.91f,-4.66f),t2=new Vector3(0,2.64f,-4.15f),t3=new Vector3(0,3.03f,-1.51f);
        hull.Quad(0,t0+Vector3.right*.12f,t1+Vector3.right*.063f,t2+Vector3.right*.063f,t3+Vector3.right*.19f);
        hull.Quad(0,t3-Vector3.right*.19f,t2-Vector3.right*.063f,t1-Vector3.right*.063f,t0-Vector3.right*.12f);
        hull.Tube(1,t0,t1,.12f,.061f);
        var legs=new Transform[4];
        for(int i=0;i<4;i++)
        {
            Quaternion turn=Quaternion.Euler(0,45f+i*90f,0);
            Vector3 bell=turn*new Vector3(0f,0f,1.36f);
            hull.Tube(4,bell+Vector3.up*2.43f,bell+Vector3.up*.81f,.39f,.63f,14);
            hull.Tube(1,bell+Vector3.up*.81f,bell+Vector3.up*1.93f,.54f,.19f,14);
            hull.Tube(4,bell+Vector3.up*.88f,bell+Vector3.up*.71f,.659f,.618f,14);
            Transform mount=Root(root,"Landing gear bay");
            mount.localRotation=turn;
            mount.localPosition=turn*new Vector3(0,3.21f,1.92f);
            Transform leg=legs[i]=Root(mount,"Retractable landing strut");
            var strutMesh=new ExpeditionMesh(3);
            Vector3 foot=new Vector3(0,-3.05f,2.13f);
            strutMesh.Tube(0,Vector3.zero,foot,.16f,.12f);
            strutMesh.Tube(2,new Vector3(0,0,.46f),foot+new Vector3(0,.73f,-.59f),.105f,.08f);
            strutMesh.Box(1,foot,new Vector3(.96f,.288f,.86f),Quaternion.identity);
            MeshPart(leg,strutMesh.Build("Landing skid and piston"),new[]{white,graphite,nickel});
        }
        MeshPart(root,hull.Build("Shuttle thermal tiles and flight surfaces"),new[]{white,graphite,orange,glazing,nickel});
        Transform exhaust=Root(root,"Engine plumes");
        var flame=Mat("Engine plasma",new Color(.86f,.35f,.09f));
        flame.EnableKeyword("_EMISSION");
        flame.SetColor("_EmissionColor",new Color(.82f,.32f,.09f));
        for(int i=0;i<4;i++)
            Part(exhaust,PrimitiveType.Sphere,Quaternion.Euler(0,45f+i*90f,0)*new Vector3(0,.58f,1.35f),
                new Vector3(.71f,2.31f,.71f),flame);
        Light glow=exhaust.gameObject.AddComponent<Light>();
        glow.color=new Color(1f,.61f,.27f);
        glow.range=14;
        glow.intensity=8;
        glow.shadows=LightShadows.None;

        var emitter=Root(parent,"Landing dust");
        var dust=emitter.gameObject.AddComponent<ParticleSystem>();
        var main=dust.main;
        main.playOnAwake=false;
        main.loop=true;
        main.maxParticles=145;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.startLifetime=new ParticleSystem.MinMaxCurve(1.2f,2.3f);
        main.startSize=new ParticleSystem.MinMaxCurve(.23f,.63f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(.35f,1.1f);
        main.startColor=new Color(.5f,.49f,.46f,.65f);
        var shape=dust.shape;
        shape.shapeType=ParticleSystemShapeType.Circle;
        shape.radius=3.2f;
        var velocity=dust.velocityOverLifetime;
        velocity.enabled=true;
        velocity.space=ParticleSystemSimulationSpace.World;
        velocity.radial=new ParticleSystem.MinMaxCurve(3.5f,12.4f);
        velocity.y=new ParticleSystem.MinMaxCurve(.12f,.89f);
        emitter.rotation=Quaternion.Euler(90,0,0);
        var emission=dust.emission;
        emission.rateOverTime=56f;
        var renderer=emitter.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Mesh;
        renderer.mesh=exhaust.GetChild(0).GetComponent<MeshFilter>().sharedMesh;
        renderer.sharedMaterial=Mat("Regolith particles",new Color(.32f,.30f,.28f));
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        dust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        return (root,exhaust,glow,dust,legs);
    }

    static void MeshPart(Transform root,Mesh data,Material[] palette)
    {
        var part=new GameObject(data.name,typeof(MeshFilter),typeof(MeshRenderer),typeof(ExpeditionMeshLifetime));
        part.transform.SetParent(root,false);
        part.GetComponent<MeshFilter>().sharedMesh=data;
        part.GetComponent<MeshRenderer>().sharedMaterials=palette;
    }

    internal Transform Sample(Transform parent)
    {
        Transform sample=Part(parent,PrimitiveType.Cube,new Vector3(0,-.04f,.18f),
            new Vector3(.16f,.12f,.15f),Mat("Survey basalt",new Color(.33f,.29f,.25f)),Quaternion.Euler(21,36,12));
        sample.gameObject.SetActive(false);
        return sample;
    }

    Material Mat(string name,Color color) => materials(name,color);
    internal static Transform Root(Transform parent,string name)
    {
        Transform root=new GameObject(name).transform;
        root.SetParent(parent,false);
        return root;
    }

    static Transform Part(Transform parent,PrimitiveType type,Vector3 position,Vector3 scale,Material material,
        Quaternion rotation=default)
    {
        GameObject part=GameObject.CreatePrimitive(type);
        Transform tr=part.transform;
        tr.SetParent(parent,false);
        tr.localPosition=position;
        tr.localRotation=rotation==default?Quaternion.identity:rotation;
        tr.localScale=scale;
        part.GetComponent<Renderer>().sharedMaterial=material;
        Collider collider=part.GetComponent<Collider>();
        collider.enabled=false;
        UnityEngine.Object.Destroy(collider);
        return tr;
    }
}
