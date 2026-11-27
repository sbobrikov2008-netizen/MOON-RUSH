using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ConveyorTransitTests
{
    const BindingFlags Internal=BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void LoadedOreWaitsInTransitAndResumesAfterBlackout()
    {
        var type=Assembly.Load("Assembly-CSharp").GetType("ConveyorTransit",true);
        object route=new[]{Vector3.zero,Vector3.forward*12f};
        object conveyor=Activator.CreateInstance(type,Internal,null,new[]{route},null);
        MethodInfo advance=type.GetMethod("Tick",Internal);
        double Tick(float seconds,bool powered,double available,out double loaded)
        {
            object[] inputs={seconds,powered,available,0d};
            var delivered=(double)advance.Invoke(conveyor,inputs);
            loaded=(double)inputs[3];
            return delivered;
        }

        Assert.That(Tick(.4f,true,1d,out double load),Is.Zero);
        Assert.That(load,Is.GreaterThan(0d));
        Assert.That(Tick(20f,false,0d,out double takenWhileDisabled),Is.Zero);
        Assert.That(takenWhileDisabled,Is.Zero);
        Assert.That(Tick(4.9f,true,0d,out _),Is.Zero);
        Assert.That(Tick(.2f,true,0d,out _),Is.EqualTo(load).Within(.000001d));
        Assert.That(Tick(5f,true,0d,out _),Is.Zero);
    }
}
