using UnityEngine;

public enum BuildingStatus
{
    Preview,
    Built
}

public enum BuildingPowerStatus
{
    Disconnected,
    Unpowered,
    Powered
}

public sealed class BuildingInstance : MonoBehaviour
{
    [SerializeField] BuildingDefinition definition;
    [SerializeField] BuildingStatus status;

    public BuildingPowerStatus PowerStatus { get; private set; }
    public bool HasPower => PowerStatus == BuildingPowerStatus.Powered;
    public bool IsProducing => HasPower && definition.productionPerMinute > 0f;
    public double ProducedUnits { get; private set; }
    bool appliedPower;

    public BuildingDefinition Definition => definition;
    public BuildingKind Kind => definition.kind;
    public int Capacity => definition.capacity;
    public BuildingStatus Status => status;

    internal void Initialize(BuildingDefinition building, BuildingStatus state)
    {
        definition = building;
        status = state;
        if (state == BuildingStatus.Built) SetPower(BuildingPowerStatus.Disconnected);
    }

    internal void SetPower(BuildingPowerStatus state)
    {
        if (appliedPower && state == PowerStatus) return;
        bool wasPowered = HasPower;
        PowerStatus = state;
        if (appliedPower && wasPowered == HasPower) return;
        appliedPower = true;
        if (TryGetComponent(out LunarBaseModule module)) module.SetPowered(HasPower);
        if (TryGetComponent(out PressureCorridor corridor)) corridor.SetPowered(HasPower);
        if (TryGetComponent(out LunarLandingPad pad)) pad.SetPowered(HasPower);
    }

    void Update()
    {
        if (status == BuildingStatus.Built && IsProducing)
            ProducedUnits += definition.productionPerMinute * Time.deltaTime / 60d;
    }
}
