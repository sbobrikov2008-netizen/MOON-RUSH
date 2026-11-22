using UnityEngine;

[CreateAssetMenu(menuName = "MOON RUSH/Building")]
public sealed class BuildingDefinition : ScriptableObject
{
    public string title;
    [TextArea] public string description;
    public Texture2D thumbnail;
    public GameObject prefab;
    [Min(1f)] public float footprintRadius = 18f;
    [Min(0f)] public float maximumSlope = 7f;
    [Min(0f)] public float maximumRelief = 1.2f;
    [Min(1)] public int maximumCount = 1;
}
