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
    Powered,
    SwitchedOff
}

public sealed class BuildingInstance : MonoBehaviour
{
    [SerializeField] BuildingDefinition definition;
    [SerializeField] BuildingStatus status;

    public BuildingPowerStatus PowerStatus { get; private set; }
    public bool HasPower => PowerStatus == BuildingPowerStatus.Powered;
    public bool IsProducing => HasPower && definition.productionPerMinute > 0f;
    internal float ProductionMultiplier { get; set; } = 1f;
    public double ProducedUnits { get; private set; }
    public float RequiredKw { get; private set; }
    public float ReceivedKw { get; private set; }
    float restoredAt=float.NegativeInfinity;
    bool appliedPower, everPowered;
    public bool RecentlyRestored => HasPower && Time.unscaledTime-restoredAt<7f;

    public BuildingDefinition Definition => definition;
    public BuildingKind Kind => definition.kind;
    public int Capacity => definition.capacity;
    public BuildingStatus Status => status;

    internal void Initialize(BuildingDefinition building, BuildingStatus state)
    {
        definition = building;
        status = state;
        if (state == BuildingStatus.Built) SetPower(building.kind==BuildingKind.LandingPad ?
            BuildingPowerStatus.Powered : BuildingPowerStatus.Disconnected);
    }

    internal void SetPower(BuildingPowerStatus state,float requiredKw=-1f)
    {
        RequiredKw=requiredKw>=0f?requiredKw:definition.powerDemandKw;
        ReceivedKw=state==BuildingPowerStatus.Powered?RequiredKw:0f;
        if (appliedPower && state == PowerStatus) return;
        bool wasPowered = HasPower;
        PowerStatus = state;
        if(HasPower)
        {
            if(!wasPowered && everPowered) restoredAt=Time.unscaledTime;
            everPowered=true;
        }
        if (appliedPower && wasPowered == HasPower) return;
        appliedPower = true;
        if (TryGetComponent(out LunarBaseModule module)) module.SetPowered(HasPower);
        if (TryGetComponent(out PressureCorridor corridor)) corridor.SetPowered(HasPower);
        if (TryGetComponent(out LunarLandingPad pad)) pad.SetPowered(HasPower);
    }

    void Update()
    {
        if (status == BuildingStatus.Built && IsProducing)
            ProducedUnits += definition.productionPerMinute * ProductionMultiplier * Time.deltaTime / 60d;
    }
}
