using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class SolarArrayTests
{
    [Test]
    public void StationUsesTwelveArraysAndFortyPercentMoreNominalPower()
    {
        const BindingFlags members = BindingFlags.Static | BindingFlags.NonPublic;
        var assembly = Assembly.Load("Assembly-CSharp");
        var layout = assembly.GetType("SolarArrayLayout", true);
        int columns = (int)layout.GetField("Columns", members).GetRawConstantValue();
        int rows = (int)layout.GetField("Rows", members).GetRawConstantValue();
        var station = AssetDatabase.LoadMainAssetAtPath("Assets/Construction/SolarStation.asset");
        var definition = station.GetType();
        Assert.That(columns * rows, Is.EqualTo(12));
        Assert.That((float)definition.GetField("solarCapacityKw").GetValue(station), Is.EqualTo(38f * 1.4f).Within(.001f));
        float footprint = (float)definition.GetField("footprintRadius").GetValue(station);
        MethodInfo sample = layout.GetMethod("Sample", members);
        var samples = new HashSet<Vector3>();
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < columns; column++)
        for (int index = 0; index < 4; index++)
        {
            var position = (Vector3)sample.Invoke(null, new object[] {column, row, index});
            Assert.That(Mathf.Abs(position.y - 4.2f), Is.LessThan(1f));
            Assert.That(new Vector2(position.x, position.z).magnitude, Is.LessThan(footprint));
            Assert.That(samples.Add(position), Is.True);
        }
        Assert.That(samples.Count, Is.EqualTo(48));
    }
}
