using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class PressurePortRoutingTests
{
    static readonly (string prefab, int count)[] Facilities =
    {
        ("CommandPost", 3), ("Habitat", 2), ("WaterStation", 1), ("OxygenStation", 1),
        ("MaterialsStation", 1), ("Greenhouse", 2), ("DockingNode", 4), ("AirlockCapsule", 3),
        ("ScienceLab", 1), ("Refinery", 1), ("FuelWorks", 1)
    };

    const BindingFlags Internal = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<Object> owned = new List<Object>();
    static Assembly Game => Assembly.Load("Assembly-CSharp");
    static Type Model => Game.GetType("BuildingInstance", true);
    static Type Planner => Game.GetType("PressureRoute", true);
    static Type Anchor => Planner.GetNestedType("Anchor", BindingFlags.NonPublic);
    Terrain terrain;

    public static IEnumerable PortCases()
    {
        foreach (var facility in Facilities)
        for (int index = 0; index < facility.count; index++)
        foreach (string layout in new[] { "level", "raised", "raised_turn", "near_left_turn", "raised_offset" })
            yield return new TestCaseData(facility.prefab, index, layout)
                .SetName($"{facility.prefab}_port_{index}_{layout}_both_directions");
    }

    [SetUp]
    public void SetUp()
    {
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(512f, 40f, 512f) };
        owned.Add(data);
        var ground = Terrain.CreateTerrainGameObject(data);
        owned.Add(ground);
        terrain = ground.GetComponent<Terrain>();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = owned.Count - 1; i >= 0; i--) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }

    [TestCaseSource(nameof(PortCases))]
    public void PrefabPortConnectsBothWays(string prefab, int index, string layout)
    {
        var (facility, ports) = Spawn(prefab, new Vector3(240f, layout is "level" or "near_left_turn" ? .045f : .88f, 240f),
            Quaternion.Euler(0f, 29f, 0f));
        Transform door = ports[index];
        Vector3 normal = door.forward;
        Assert.That(Mathf.Abs(normal.y), Is.LessThan(.001f));
        Assert.That(Vector3.Dot(door.position - facility.transform.position, normal), Is.GreaterThan(0f));
        Vector3 side = Vector3.Cross(Vector3.up, normal);
        Vector3 approach = layout switch
        {
            "raised_turn" => -side,
            "near_left_turn" => side,
            _ => -normal
        };
        Vector3 offset = layout switch
        {
            "raised_turn" => normal * 10f + side * 20f,
            "near_left_turn" => normal * 11f - side * 19f,
            "raised_offset" => normal * 16f + side * 6.25f,
            _ => normal * 28f
        };
        var (target, targetPorts) = Spawn("DockingNode", Vector3.zero, Quaternion.LookRotation(approach));
        Transform arrival = targetPorts[0];
        Vector3 position = door.position + offset - arrival.position;
        position.y = .045f;
        target.transform.position = position;
        AssertBothDirections(facility, door, target, arrival);
    }

    [TestCase("CommandPost")]
    [TestCase("Habitat")]
    [TestCase("MaterialsStation")]
    public void PrimaryDoorOnRaisedFoundationCanTurn(string prefab)
    {
        var (facility, ports) = Spawn(prefab, new Vector3(240f, .88f, 240f), Quaternion.identity);
        Transform door = ports[0];
        Vector3 side = Vector3.Cross(Vector3.up, door.forward);
        var (target, targetPorts) = Spawn("DockingNode", Vector3.zero, Quaternion.LookRotation(-side));
        Vector3 position = door.position + door.forward * 10f + side * 20f - targetPorts[0].position;
        position.y = .045f;
        target.transform.position = position;
        AssertBothDirections(facility, door, target, targetPorts[0]);
    }

    [Test]
    public void ExternalObstacleNameIsReportedAndSearchTerminates()
    {
        var (facility, ports) = Spawn("CommandPost", new Vector3(240f, .045f, 240f), Quaternion.identity);
        Transform door = ports[0];
        var (target, targetPorts) = Spawn("DockingNode", Vector3.zero, Quaternion.LookRotation(-door.forward));
        Vector3 position = door.position + door.forward * 28f - targetPorts[0].position;
        position.y = .045f;
        target.transform.position = position;
        var (blocker, _) = Spawn("Habitat", door.position + door.forward * 3f, Quaternion.identity);
        blocker.GetComponent<BoxCollider>().size = new Vector3(3f, 4f, 3f);
        blocker.GetComponent<BoxCollider>().center = Vector3.zero;
        Assert.That(Find(facility, door, target, targetPorts[0], out var failure, blocker), Is.Null);
        Assert.That(failure, Does.Contain("Жилой комплекс"));
    }

    [Test]
    public void RoutesAroundAnObstacleAndCanCrossPowerCable()
    {
        var (facility, ports) = Spawn("CommandPost", new Vector3(240f,.045f,210f),Quaternion.identity);
        Transform door=ports[0];
        var (target, targetPorts) = Spawn("DockingNode",new Vector3(240f,.045f,280f),Quaternion.Euler(0f,180f,0f));
        var (blocker, _) = Spawn("Habitat",new Vector3(240f,0f,248f),Quaternion.identity);
        blocker.GetComponent<BoxCollider>().size=new Vector3(8f,4f,7f);
        blocker.GetComponent<BoxCollider>().center=Vector3.zero;

        var powerCable=new GameObject("Electrical cable proxy",typeof(BoxCollider));
        owned.Add(powerCable);
        powerCable.layer=2;
        powerCable.transform.position=new Vector3(240f,.08f,260f);
        powerCable.GetComponent<BoxCollider>().size=new Vector3(80f,.16f,.2f);

        Vector3[] forward=Find(facility,door,target,targetPorts[0],out string reason,blocker);
        Assert.That(forward,Is.Not.Null,reason);
        Assert.That(System.Array.Exists(forward,point=>Mathf.Abs(point.x-240f)>5.55f),Is.True);
        Vector3[] reverse=Find(target,targetPorts[0],facility,door,out string backwardReason,blocker);
        Assert.That(reverse,Is.Not.Null,backwardReason);
        Assert.That(reverse.Length,Is.EqualTo(forward.Length));
        for(int i=0;i<forward.Length;i++)
            Assert.That(reverse[reverse.Length-1-i],Is.EqualTo(forward[i]));
    }

    void AssertBothDirections(Component a, Transform start, Component b, Transform finish)
    {
        Vector3[] forward = Find(a, start, b, finish, out var forwardReason);
        Assert.That(forward, Is.Not.Null, forwardReason);
        Assert.That(Vector3.Distance(forward[0], start.position), Is.LessThan(.002f));
        Assert.That(Vector3.Distance(forward[forward.Length - 1], finish.position), Is.LessThan(.002f));
        Vector3[] backward = Find(b, finish, a, start, out var backwardReason);
        Assert.That(backward, Is.Not.Null, backwardReason);
        Assert.That(backward.Length, Is.EqualTo(forward.Length));
        for(int i=0;i<forward.Length;i++)
            Assert.That(Vector3.Distance(backward[backward.Length-1-i],forward[i]),Is.LessThan(.002f));
    }

    Vector3[] Find(Component a, Transform start, Component b, Transform finish, out string reason,
        Component blocker = null)
    {
        Array buildings = Array.CreateInstance(Model, blocker == null ? 2 : 3);
        buildings.SetValue(a, 0);
        buildings.SetValue(b, 1);
        if (blocker != null) buildings.SetValue(blocker, 2);
        object[] args = { terrain, Activator.CreateInstance(Anchor, a, start),
            Activator.CreateInstance(Anchor, b, finish), buildings,
            Array.CreateInstance(Game.GetType("PressureCorridor", true), 0), null };
        var route = (Vector3[])Planner.GetMethod("Find").Invoke(null, args);
        reason = (string)args[5];
        return route;
    }

    (Component, Transform[]) Spawn(string prefab, Vector3 position, Quaternion rotation)
    {
        string path = $"Assets/Construction/{prefab}";
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path + ".prefab"));
        owned.Add(root);
        root.transform.SetPositionAndRotation(position, rotation);
        var instance = root.AddComponent(Model);
        Model.GetField("definition", Internal).SetValue(instance, AssetDatabase.LoadMainAssetAtPath(path + ".asset"));
        Model.GetField("status", Internal).SetValue(instance, Enum.Parse(Game.GetType("BuildingStatus", true), "Built"));
        var module = root.GetComponent(Game.GetType("LunarBaseModule", true));
        return (instance, (Transform[])module.GetType().GetField("connectionPorts", Internal).GetValue(module));
    }
}
