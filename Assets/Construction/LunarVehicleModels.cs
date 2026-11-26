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
        internal Transform HydraulicCylinder, HydraulicRod, HydraulicBase, HydraulicTip;
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
        root.localScale=Vector3.one*1.875f;
        Material chassis=Mat("Rover alloy",new Color(.69f,.72f,.73f));
        Material dark=Mat("Rover structure",new Color(.14f,.17f,.20f));
        Material tread=Mat("Rover tread",new Color(.075f,.09f,.102f));
        Material blue=Mat("Rover photovoltaic cells",new Color(.11f,.25f,.44f));
        Material orange=Mat("Rover navy",new Color(.12f,.23f,.36f));
        Part(root,PrimitiveType.Cube,new Vector3(0,.67f,0),new Vector3(2.19f,.39f,3.03f),dark);
        Part(root,PrimitiveType.Cube,new Vector3(0,.96f,.16f),new Vector3(1.87f,.52f,2.64f),chassis);
        Part(root,PrimitiveType.Cube,new Vector3(0,1.05f,1.49f),new Vector3(1.57f,.25f,.29f),orange);
        Part(root,PrimitiveType.Cylinder,new Vector3(-.33f,1.50f,.77f),new Vector3(.14f,.44f,.14f),chassis);
        Part(root,PrimitiveType.Cube,new Vector3(-.33f,1.99f,.84f),new Vector3(.52f,.24f,.31f),dark);
        Part(root,PrimitiveType.Sphere,new Vector3(-.33f,1.99f,1.00f),Vector3.one*.15f,blue);
        for(int side=-1;side<=1;side+=2)
        {
            Part(root,PrimitiveType.Cylinder,new Vector3(-.33f+side*.158f,2.005f,1.02f),new Vector3(.105f,.075f,.105f),dark,Quaternion.Euler(90,0,0));
            Part(root,PrimitiveType.Cube,new Vector3(side*.81f,1.01f,1.37f),new Vector3(.26f,.22f,.29f),chassis);
        }
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
        claw.localPosition=new Vector3(0,0,.57f);
        Part(claw,PrimitiveType.Cube,Vector3.zero,new Vector3(.49f,.22f,.31f),alloy);
        Part(claw,PrimitiveType.Cylinder,new Vector3(0,.012f,.075f),new Vector3(.24f,.24f,.24f),dark,Quaternion.Euler(0,0,90));
        Transform hydraulicBase=Root(basePivot,"Hydraulic base pin");
        hydraulicBase.localPosition=new Vector3(.155f,.12f,-.19f);
        Transform hydraulicTip=Root(shoulder,"Hydraulic arm pin");
        hydraulicTip.localPosition=new Vector3(.155f,-.08f,.46f);
        Transform cylinder=Part(root,PrimitiveType.Cylinder,Vector3.zero,new Vector3(.14f,.3f,.14f),dark);
        Transform rod=Part(root,PrimitiveType.Cylinder,Vector3.zero,new Vector3(.075f,.3f,.075f),chassis);
        Transform left=Finger(-1), right=Finger(1);
        return new Model{Root=root,Joints=wheels,TurnPivots=steering,Secondary=new[]{basePivot,shoulder,elbow},
            Tool=claw,JawLeft=left,JawRight=right,HydraulicBase=hydraulicBase,HydraulicTip=hydraulicTip,
            HydraulicCylinder=cylinder,HydraulicRod=rod};

        Transform Finger(float side)
        {
            Transform finger=Root(claw,"Claw jaw");
            finger.localPosition=new Vector3(side*.22f,0,.04f);
            var jaw=new ExpeditionMesh(3);
            jaw.Box(0,new Vector3(side*.031f,0,.155f),new Vector3(.16f,.19f,.42f),Quaternion.Euler(0,side*11f,0));
            jaw.Box(1,new Vector3(-side*.045f,0,.348f),new Vector3(.25f,.177f,.18f),Quaternion.Euler(0,-side*30f,0));
            for(int tooth=0;tooth<3;tooth++)
                jaw.Box(2,new Vector3(-side*(.08f+tooth*.035f),0,.235f+tooth*.068f),
                    new Vector3(.126f,.14f,.045f),Quaternion.identity);
            MeshPart(finger,jaw.Build("Forged sample jaw"),new[]{alloy,dark,chassis});
            return finger;
        }
    }

    internal (Transform root, Transform exhaust, Light glow, Transform[] legs) Rocket(Transform parent)
    {
        Transform root=Root(parent,"Лунная ракета вертикальной посадки");
        Material white=Mat("Rocket ceramic",new Color(.86f,.88f,.88f));
        Material graphite=Mat("Rocket bands",new Color(.072f,.084f,.105f));
        Material marking=Mat("Rocket identification",new Color(.32f,.43f,.52f));
        Material glazing=Mat("Cockpit obsidian glass",new Color(.066f,.12f,.19f));
        glazing.SetFloat("_Smoothness",.76f);
        Material nickel=Mat("Engine nickel alloy",new Color(.51f,.55f,.58f));
        nickel.SetFloat("_Metallic",.62f);
        var hull=new ExpeditionMesh(5);
        const int sides=32;
        var rings=new (float y,float r,int finish)[]
        {
            (2.18f,2.22f,4),(2.77f,2.50f,4),(3.33f,2.54f,1),(4.38f,2.54f,1),
            (4.44f,2.51f,4),(5.70f,2.51f,0),(8.86f,2.50f,0),(9.18f,2.50f,1),
            (14.17f,2.48f,0),(15.40f,2.47f,1),(17.34f,2.43f,0),(20.48f,2.38f,0),
            (21.38f,2.25f,0),(22.31f,1.98f,1),(23.54f,1.62f,0),(24.74f,1.12f,0),
            (25.82f,.61f,0),(26.57f,.24f,0),(26.84f,.014f,4)
        };
        for(int j=1;j<rings.Length;j++)
        for(int i=0;i<sides;i++)
        {
            bool window=j==13 && (i is 5 or 7 or 9 or 11);
            hull.Quad(window?3:rings[j].finish,R(j-1,i),R(j,i),R(j,i+1),R(j-1,i+1));
        }
        for(int i=0;i<sides;i++) hull.Face(1,Vector3.up*rings[0].y,R(0,i),R(0,i+1));
        Vector3 R(int j,int i)
        {
            float angle=i*Mathf.PI*2f/sides;
            return new Vector3(rings[j].r*Mathf.Cos(angle),rings[j].y,rings[j].r*Mathf.Sin(angle));
        }
        for(int rail=0;rail<4;rail++)
        {
            Quaternion turn=Quaternion.Euler(0,45f+rail*90f,0);
            hull.Box(1,turn*new Vector3(0f,6.34f,2.47f),new Vector3(.43f,4.06f,.15f),turn);
            for(int clip=0;clip<8;clip++)
                hull.Box(4,turn*new Vector3(0f,4.72f+clip*.466f,2.572f),new Vector3(.48f,.085f,.11f),turn);
            hull.Box(2,turn*new Vector3(0f,10.52f,2.508f),new Vector3(1.15f,.18f,.044f),turn);
        }

        Transform exhaust=Root(root,"Engine flame core");
        var flame=Mat("Rocket flame amber",new Color(.91f,.36f,.10f));
        var core=Mat("Rocket flame core",new Color(.94f,.68f,.39f));
        flame.EnableKeyword("_EMISSION");
        core.EnableKeyword("_EMISSION");
        flame.SetColor("_EmissionColor",new Color(.77f,.24f,.055f));
        core.SetColor("_EmissionColor",new Color(.90f,.54f,.25f));
        var plumes=new ExpeditionMesh(2);
        for(int i=0;i<5;i++)
        {
            Quaternion bearing=Quaternion.Euler(0,i*90f,0);
            Vector3 bell=i==4?Vector3.zero:bearing*new Vector3(.88f,0f,.88f);
            float size=i==4?1.24f:1f;
            hull.Tube(4,bell+Vector3.up*2.25f,bell+Vector3.up*1.42f,.37f*size,.41f*size,16);
            hull.Tube(4,bell+Vector3.up*1.42f,bell+Vector3.up*.74f,.41f*size,.63f*size,16);
            hull.Tube(1,bell+Vector3.up*.74f,bell+Vector3.up*1.70f,.58f*size,.23f*size,16);
            for(int rib=0;rib<3;rib++)
            {
                float y=.78f+rib*.19f,r=(.649f-rib*.05f)*size;
                hull.Tube(4,bell+Vector3.up*y,bell+Vector3.up*(y+.045f),r,r,16);
            }
            Vector3 tip=bell+Vector3.down*(2.75f*size);
            plumes.Tube(0,bell+Vector3.up*.67f,bell-Vector3.up*.33f,.27f*size,.46f*size,12);
            plumes.Tube(0,bell-Vector3.up*.33f,tip,.46f*size,.015f,12);
            // The warm inner flame continues a little past the bell but remains below bloom threshold.
            plumes.Tube(1,bell+Vector3.up*.63f,bell-Vector3.up*.22f,.286f*size,.25f*size,12);
        }
        MeshPart(exhaust,plumes.Build("Five tapered exhaust flames"),new[]{flame,core});
        var legs=new Transform[4];
        for(int i=0;i<legs.Length;i++)
        {
            Quaternion turn=Quaternion.Euler(0,45f+i*90f,0);
            Transform mount=Root(root,"Landing gear hinge");
            mount.localRotation=turn;
            mount.localPosition=turn*new Vector3(0,3.24f,2.32f);
            Transform leg=legs[i]=Root(mount,"Retractable landing leg");
            var strut=new ExpeditionMesh(3);
            Vector3 foot=new Vector3(0,-3.07f,1.89f);
            strut.Tube(0,Vector3.zero,foot,.17f,.127f);
            strut.Tube(2,new Vector3(0,0,.39f),foot+new Vector3(0,.74f,-.52f),.108f,.081f);
            strut.Box(1,foot,new Vector3(.91f,.30f,.86f),Quaternion.identity);
            MeshPart(leg,strut.Build("Damper and landing foot"),new[]{white,graphite,nickel});
        }
        MeshPart(root,hull.Build("Rocket body and engine assembly"),new[]{white,graphite,marking,glazing,nickel});
        Light glow=exhaust.gameObject.AddComponent<Light>();
        glow.color=new Color(1f,.58f,.26f);
        glow.range=12f;
        glow.intensity=5.4f;
        glow.shadows=LightShadows.None;
        return (root,exhaust,glow,legs);
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
        Transform sample=Part(parent,PrimitiveType.Cube,new Vector3(0,-.035f,.37f),
            new Vector3(.243f,.168f,.213f),Mat("Survey basalt",new Color(.33f,.29f,.25f)),Quaternion.Euler(21,36,12));
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
