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
        Part(body,PrimitiveType.Sphere,new Vector3(0,1.68f,0),Vector3.one*(suited?.43f:.27f),suited?fabric:face);
        Part(body,PrimitiveType.Sphere,new Vector3(0,1.69f,.126f),new Vector3(.344f,.276f,.20f),
            suited?Mat("Visor gold",new Color(.38f,.25f,.12f)):face).gameObject.SetActive(suited);
        if (!suited) Part(body,PrimitiveType.Sphere,new Vector3(0,1.77f,-.025f),new Vector3(.26f,.16f,.27f),boot);
        if (suited)
        {
            Part(body,PrimitiveType.Cube,new Vector3(0,1.26f,-.239f),new Vector3(.41f,.56f,.25f),fabric);
            Part(body,PrimitiveType.Cylinder,new Vector3(.23f,1.22f,-.246f),new Vector3(.15f,.235f,.15f),trim);
            Part(body,PrimitiveType.Cube,new Vector3(0,1.28f,.185f),new Vector3(.23f,.22f,.10f),boot);
            Part(body,PrimitiveType.Cube,new Vector3(0,1.33f,.242f),new Vector3(.15f,.05f,.016f),trim);
        }
        var joints = new Transform[4];
        for (int i=0;i<4;i++)
        {
            bool arm=i>=2;
            float side=i%2==0?-1f:1f;
            var joint=joints[i]=Root(body,arm?"Shoulder":"Hip");
            joint.localPosition=new Vector3(side*(arm?.29f:.12f),arm?1.41f:.97f,0);
            Part(joint,PrimitiveType.Capsule,new Vector3(0,arm?-.24f:-.35f,0),
                arm?new Vector3(.15f,.245f,.15f):new Vector3(.20f,.35f,.21f),fabric);
            Part(joint,PrimitiveType.Sphere,new Vector3(0,arm?-.48f:-.75f,arm?0f:.04f),
                arm?new Vector3(.14f,.16f,.15f):new Vector3(.21f,.18f,.33f),boot);
            if (suited) Part(joint,PrimitiveType.Cube,new Vector3(0,arm?-.32f:-.41f,.076f),
                new Vector3(.13f,.078f,.063f),trim);
        }
        return new Model { Root=root,Joints=joints,Steering=body };
    }

    internal Model Rover(Transform parent)
    {
        var root=Root(parent,"Восьмиколёсный исследовательский луноход");
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
        var wheels=new Transform[8];
        for(int i=0;i<wheels.Length;i++)
        {
            float side=i<4?-1f:1f, z=(i%4-1.5f)*.83f;
            var joint=wheels[i]=Root(root,"Wheel");
            joint.localPosition=new Vector3(side*1.16f,.42f,z);
            Part(joint,PrimitiveType.Cylinder,Vector3.zero,new Vector3(.85f,.18f,.85f),tread,
                Quaternion.Euler(0,0,90));
            Part(joint,PrimitiveType.Cylinder,new Vector3(side*.195f,0,0),new Vector3(.48f,.038f,.48f),chassis,
                Quaternion.Euler(0,0,90));
            for(int lug=0;lug<8;lug++)
            {
                Quaternion angle=Quaternion.Euler(lug*45f,0,0);
                Part(joint,PrimitiveType.Cube,angle*new Vector3(0,.419f,0),new Vector3(.35f,.047f,.125f),dark,angle);
            }
            Part(root,PrimitiveType.Cube,new Vector3(side*.95f,.52f,z),new Vector3(.53f,.1f,.15f),orange,
                Quaternion.Euler(0,0,side*-18f));
        }
        var drill=Part(root,PrimitiveType.Cylinder,new Vector3(.38f,.56f,1.71f),new Vector3(.21f,.43f,.21f),chassis);
        return new Model{Root=root,Joints=wheels,Steering=drill};
    }

    internal (Transform root, Transform exhaust, Light glow, ParticleSystem dust) Rocket(Transform parent)
    {
        Transform root=Root(parent,"Лунная транспортная ракета");
        Material white=Mat("Rocket ceramic",new Color(.75f,.78f,.81f));
        Material graphite=Mat("Rocket heat shield",new Color(.15f,.17f,.21f));
        Material orange=Mat("Suit orange",new Color(.88f,.37f,.095f));
        Part(root,PrimitiveType.Cylinder,new Vector3(0,10.25f,0),new Vector3(5.5f,7.2f,5.5f),white);
        Part(root,PrimitiveType.Sphere,new Vector3(0,17.3f,0),new Vector3(5.5f,7.4f,5.5f),white);
        Part(root,PrimitiveType.Cylinder,new Vector3(0,3.05f,0),new Vector3(5.65f,.62f,5.65f),graphite);
        Part(root,PrimitiveType.Cylinder,new Vector3(0,14.97f,0),new Vector3(5.62f,.23f,5.62f),orange);
        for(int i=0;i<4;i++)
        {
            Quaternion turn=Quaternion.Euler(0,45f+i*90f,0);
            Part(root,PrimitiveType.Cylinder,turn*new Vector3(0,2.12f,1.35f),new Vector3(1.0f,.65f,1.0f),graphite);
            Part(root,PrimitiveType.Cube,turn*new Vector3(0,2.03f,3.05f),new Vector3(.28f,4.10f,.35f),white,
                turn*Quaternion.Euler(45f,0,0));
            Part(root,PrimitiveType.Cylinder,turn*new Vector3(0,.16f,4.65f),new Vector3(1.63f,.16f,1.28f),graphite);
            Part(root,PrimitiveType.Sphere,turn*new Vector3(0,16.3f,2.69f),new Vector3(.67f,.49f,.18f),graphite,turn);
        }
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
        main.startSpeed=new ParticleSystem.MinMaxCurve(2f,6f);
        main.startColor=new Color(.5f,.49f,.46f,.65f);
        var shape=dust.shape;
        shape.shapeType=ParticleSystemShapeType.Circle;
        shape.radius=1.2f;
        emitter.rotation=Quaternion.Euler(90,0,0);
        var emission=dust.emission;
        emission.rateOverTime=42f;
        var renderer=dust.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Mesh;
        renderer.mesh=root.GetChild(1).GetComponent<MeshFilter>().sharedMesh;
        renderer.sharedMaterial=Mat("Regolith particles",new Color(.32f,.30f,.28f));
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        dust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        return (root,exhaust,glow,dust);
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
