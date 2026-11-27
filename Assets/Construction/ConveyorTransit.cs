using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class ConveyorTransit
{
    internal const float Speed = 2.4f;
    internal const int RockLimit = 72;
    internal readonly struct Load
    {
        internal readonly double Start, Ore;
        internal readonly int Id;
        internal Load(double start,double ore,int id) { Start=start; Ore=ore; Id=id; }
    }

    readonly Queue<Load> loads=new Queue<Load>();
    readonly double length, spacing;
    double distance, staging;
    int sequence;
    internal bool Moving { get; private set; }
    internal Queue<Load> Loads => loads;
    internal float Length => (float)length;
    internal double Position => distance;

    internal ConveyorTransit(Vector3[] route)
    {
        for(int i=1;i<route.Length;i++) length+=Vector3.Distance(route[i-1],route[i]);
        length=Math.Max(1d,length);
        spacing=Math.Max(1.65d,length/(RockLimit-2d));
    }

    internal double Tick(float seconds,bool powered,double available,out double taken)
    {
        taken=0d;
        Moving=powered && (available>0d || staging>0d || loads.Count>0);
        if(!Moving || seconds<=0f) return 0d;
        distance+=seconds*Speed;
        double delivered=0d;
        while(loads.Count>0 && distance-loads.Peek().Start>=length) delivered+=loads.Dequeue().Ore;

        taken=Math.Min(available,4d*seconds/60d);
        staging+=taken;
        if(staging>0d && distance>=(sequence==0?0d:latestStart+spacing))
        {
            loads.Enqueue(new Load(distance,staging,sequence++));
            latestStart=distance;
            staging=0d;
        }
        return delivered;
    }
    double latestStart;

    internal void SetPower(bool powered)
    {
        if(!powered) Moving=false;
    }

    internal double Recover()
    {
        double ore=staging;
        staging=0d;
        while(loads.Count>0) ore+=loads.Dequeue().Ore;
        Moving=false;
        return ore;
    }
}
