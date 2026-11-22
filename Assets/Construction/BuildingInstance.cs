using UnityEngine;

public enum BuildingStatus
{
    Preview,
    Built
}

public sealed class BuildingInstance : MonoBehaviour
{
    [SerializeField] BuildingDefinition definition;
    [SerializeField] BuildingStatus status;

    public BuildingDefinition Definition => definition;
    public BuildingKind Kind => definition.kind;
    public int Capacity => definition.capacity;
    public BuildingStatus Status => status;

    internal void Initialize(BuildingDefinition building, BuildingStatus state)
    {
        definition = building;
        status = state;
    }
}
