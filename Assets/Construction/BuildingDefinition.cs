using UnityEngine;

public enum BuildingKind
{
    LandingPad,
    CommandPost,
    Habitat,
    SolarStation,
    WaterStation,
    OxygenStation,
    Greenhouse
}

[CreateAssetMenu(menuName = "MOON RUSH/Building")]
public sealed class BuildingDefinition : ScriptableObject
{
    public BuildingKind kind;
    public string title;
    [TextArea] public string description;
    [TextArea] public string inspectionDescription;
    [TextArea] public string characteristics;
    public Texture2D thumbnail;
    public GameObject prefab;
    [Min(1f)] public float footprintRadius = 18f;
    [Min(0f)] public float maximumSlope = 7f;
    [Min(0f)] public float maximumRelief = 1.2f;
    [Min(0)] public int capacity;
    [Tooltip("Zero allows any number of buildings of this type.")]
    [Min(0)] public int maximumCount = 1;
}
