using System.Reflection;
using NUnit.Framework;

public sealed class LandingFlightTests
{
    [TestCase(4.99f, false, false, 0f, 0f)]
    [TestCase(5f, true, true, 229.9f, 230.1f)]
    [TestCase(16.95f, true, true, 0f, .01f)]
    [TestCase(17f, true, false, 0f, 0f)]
    [TestCase(46.99f, true, false, 0f, 0f)]
    [TestCase(51f, true, true, 20f, 50f)]
    [TestCase(57f, false, false, 0f, 0f)]
    public void FlightFollowsPadClock(float elapsed, bool visible, bool firing, float minHeight, float maxHeight)
    {
        var operations = Assembly.Load("Assembly-CSharp").GetType("LunarColonyOperations", true);
        MethodInfo evaluate = operations.GetMethod("TryFlight", BindingFlags.Static | BindingFlags.NonPublic);
        object[] arguments = { elapsed, 0f, false };
        Assert.That(evaluate.Invoke(null, arguments), Is.EqualTo(visible));
        Assert.That(arguments[1], Is.InRange(minHeight, maxHeight));
        Assert.That(arguments[2], Is.EqualTo(firing));
    }

    [TestCase(0f, 0f)]
    [TestCase(3f, 0f)]
    [TestCase(20.5f, 68.5f)]
    [TestCase(38f, 137f)]
    [TestCase(190f, 137f)]
    public void LandingGearTransitionsBetweenContactAndStowed(float height, float angle)
    {
        var operations = Assembly.Load("Assembly-CSharp").GetType("LunarColonyOperations", true);
        MethodInfo evaluate = operations.GetMethod("LandingGearAngle", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That((float)evaluate.Invoke(null, new object[] { height }), Is.EqualTo(angle).Within(.002f));
    }
}
