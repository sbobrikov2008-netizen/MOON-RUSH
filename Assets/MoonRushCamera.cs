using UnityEngine;

[RequireComponent(typeof(Camera))]
public class MoonRushCamera : MonoBehaviour
{
    [SerializeField] Vector3 target = new Vector3(0f, 8f, 0f);
    [SerializeField] float yaw = 35f;
    [SerializeField, Range(.1f, .45f)] float mapHalfExtent = .40f;
    [SerializeField] float zoomHeightRange = 92f;
    [SerializeField] float startZoomHeight = 29f;
    [SerializeField] Terrain terrain;

    const float Pitch = 25f;
    const float GroundClearance = 8f;
    const float MaxViewAspect = 32f / 9f;

    Camera view;
    Quaternion rotation;
    Vector2 heading;
    Vector2 center;
    Vector2 position;
    Vector2 panGoal;
    float halfExtent;
    float minimumHeight;
    float zoomHeight;
    float zoomGoal;

    void Awake()
    {
        view = GetComponent<Camera>();
        rotation = Quaternion.Euler(Pitch, yaw, 0f);
        Vector3 forward = rotation * Vector3.forward;
        heading = new Vector2(forward.x, forward.z).normalized;

        TerrainData data = terrain != null ? terrain.terrainData : null;
        if (data != null)
        {
            Vector3 origin = terrain.transform.position;
            center = new Vector2(origin.x + data.size.x * .5f, origin.z + data.size.z * .5f);
            halfExtent = Mathf.Min(data.size.x, data.size.z) * mapHalfExtent;
            minimumHeight = origin.y + HighestGround(data) + GroundClearance;
        }
        else
        {
            center = new Vector2(target.x, target.z);
            halfExtent = 360f;
            minimumHeight = target.y + GroundClearance;
        }

        zoomHeight = zoomGoal = Mathf.Clamp(startZoomHeight, 0f, zoomHeightRange);
        Vector2 focus = new Vector2(target.x, target.z);
        float groundY = data != null ? terrain.transform.position.y + terrain.SampleHeight(target) : target.y;
        position = panGoal = Clamp(focus - heading * HorizontalTravel(minimumHeight + zoomHeight - groundY));
        UpdateViewport();
        ApplyPose();
    }

    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        float blend = 1f - Mathf.Exp(-13f * dt);
        float wheel = Input.mouseScrollDelta.y;
        if (wheel != 0f)
            zoomGoal = Mathf.Clamp(zoomGoal - wheel * 9f, 0f, zoomHeightRange);
        float previousHeight = zoomHeight;
        zoomHeight = Mathf.Lerp(zoomHeight, zoomGoal, blend);
        if (Mathf.Abs(zoomHeight - zoomGoal) < .001f) zoomHeight = zoomGoal;

        // Dolly toward the current view on the wheel. The arrow input itself never alters Y.
        Vector2 dolly = -heading * HorizontalTravel(zoomHeight - previousHeight);
        position = Clamp(position + dolly);
        panGoal = Clamp(panGoal + dolly);

        float x = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        float z = (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
        float speed = Mathf.Lerp(105f, 205f, zoomHeight / Mathf.Max(1f, zoomHeightRange));
        Vector2 right = new Vector2(heading.y, -heading.x);
        Vector2 movement = Vector2.ClampMagnitude(right * x + heading * z, 1f);
        panGoal = Clamp(panGoal + movement * speed * dt);
        position = Clamp(Vector2.Lerp(position, panGoal, blend));
        if ((position - panGoal).sqrMagnitude < .0001f) position = panGoal;

        UpdateViewport();
        ApplyPose();
    }

    float HighestGround(TerrainData data)
    {
        // Include the samples just outside the square: its edges can fall inside a heightfield cell.
        int cells = data.heightmapResolution - 1;
        int x0 = Mathf.FloorToInt((.5f - halfExtent / data.size.x) * cells);
        int z0 = Mathf.FloorToInt((.5f - halfExtent / data.size.z) * cells);
        int x1 = Mathf.CeilToInt((.5f + halfExtent / data.size.x) * cells);
        int z1 = Mathf.CeilToInt((.5f + halfExtent / data.size.z) * cells);
        float peak = 0f;
        foreach (float height in data.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1))
            peak = Mathf.Max(peak, height);
        return peak * data.size.y;
    }

    Vector2 Clamp(Vector2 point) => new Vector2(
        Mathf.Clamp(point.x, center.x - halfExtent, center.x + halfExtent),
        Mathf.Clamp(point.y, center.y - halfExtent, center.y + halfExtent));

    static float HorizontalTravel(float height) => height / Mathf.Tan(Pitch * Mathf.Deg2Rad);

    void UpdateViewport()
    {
        float screenAspect = Screen.width / Mathf.Max(1f, Screen.height);
        float aspect = Mathf.Min(screenAspect, MaxViewAspect);
        float width = Mathf.Clamp01(aspect / Mathf.Max(.001f, screenAspect));
        view.rect = new Rect((1f - width) * .5f, 0f, width, 1f);
        view.aspect = aspect;
    }

    void ApplyPose() => transform.SetPositionAndRotation(
        new Vector3(position.x, minimumHeight + zoomHeight, position.y), rotation);
}
