using UnityEngine;

[RequireComponent(typeof(Camera))]
public class MoonRushCamera : MonoBehaviour
{
    [SerializeField] Vector3 target = new Vector3(0f, 23f, 0f);
    [SerializeField] float distance = 900f;
    [SerializeField] float startDistance = 520f;
    [SerializeField] float yaw = 35f;
    const float Pitch = 25f;
    const float EdgeMargin = 16f;
    const float MaxViewAspect = 32f / 9f;
    const int ValleySides = 32;
    readonly Vector2[] valleyNormals = new Vector2[ValleySides];
    readonly float[] valleyLimits = new float[ValleySides];
    TerrainData valleyData;
    Vector2 valleyCenter;
    [SerializeField] float minDistance = 90f;
    [SerializeField] float playableHalfSize = 290f;
    [SerializeField] Terrain terrain;

    Camera view;
    float zoomGoal;
    float renderedDistance;
    Vector3 panGoal;

    void Awake()
    {
        view = GetComponent<Camera>();
        panGoal = target;
        zoomGoal = startDistance;
        UpdatePose(0f, true);
    }

    void LateUpdate()
    {
        float wheel = Input.mouseScrollDelta.y;
        if (wheel != 0f)
            zoomGoal = Mathf.Clamp(zoomGoal * Mathf.Exp(-wheel * .12f), minDistance, distance);
        float x = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        float z = (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
        Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
        var keys = new Vector3(x, 0f, z);
        panGoal += heading * Vector3.ClampMagnitude(keys, 1f) *
            (renderedDistance * .62f * Time.unscaledDeltaTime);
        UpdatePose(Time.unscaledDeltaTime);
    }

    void UpdatePose(float dt, bool initialize = false)
    {
        float blend = dt > 0f ? 1f - Mathf.Exp(-13f * dt) : 1f;
        Quaternion rotation = Quaternion.Euler(Pitch, yaw, 0f);
        TerrainData data = terrain != null ? terrain.terrainData : null;
        float screenAspect = Screen.width / Mathf.Max(1f, Screen.height);
        float aspect = Mathf.Min(screenAspect, MaxViewAspect);
        float viewportWidth = Mathf.Clamp01(aspect / Mathf.Max(.001f, screenAspect));
        view.rect = new Rect((1f - viewportWidth) * .5f, 0f, viewportWidth, 1f);
        view.aspect = aspect;

        if (data == null)
        {
            target = Vector3.Lerp(target, panGoal, blend);
            renderedDistance = Mathf.Lerp(renderedDistance, zoomGoal, blend);
            transform.SetPositionAndRotation(target - rotation * Vector3.forward * renderedDistance, rotation);
            return;
        }

        Vector3 origin = terrain.transform.position;
        Vector3 center = origin + data.size * .5f;
        // Pan never follows sampled ground. The same fixed pivot Y is used for every pose.
        center.y = target.y;
        if (data != valleyData) BuildValley(data, center);
        float panLimit = playableHalfSize;
        panGoal.x = Mathf.Clamp(panGoal.x, center.x - panLimit, center.x + panLimit);
        panGoal.z = Mathf.Clamp(panGoal.z, center.z - panLimit, center.z + panLimit);
        float vertical = Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad);

        // Use the widest supported view to set a zoom cap independent of pan and window size.
        float upper = distance;
        float lower = minDistance;
        if (!TryPose(center, upper, MaxViewAspect, out _))
        {
            for (int step = 0; step < 12; step++)
            {
                float candidate = (lower + upper) * .5f;
                if (TryPose(center, candidate, MaxViewAspect, out _)) lower = candidate;
                else upper = candidate;
            }
            upper = lower;
        }
        zoomGoal = Mathf.Clamp(zoomGoal, minDistance, upper);
        renderedDistance = initialize ? zoomGoal : Mathf.Lerp(renderedDistance, zoomGoal, blend);

        Vector3 proposed = Vector3.Lerp(target, panGoal, blend);
        if (!TryPose(proposed, renderedDistance, aspect, out Vector3 position))
        {
            Vector3 safe = TryPose(target, renderedDistance, aspect, out _) ? target : center;
            float good = 0f;
            float blocked = 1f;
            for (int step = 0; step < 12; step++)
            {
                float candidate = (good + blocked) * .5f;
                if (TryPose(Vector3.Lerp(safe, proposed, candidate), renderedDistance, aspect, out _))
                    good = candidate;
                else blocked = candidate;
            }
            proposed = Vector3.Lerp(safe, proposed, good);
            panGoal = proposed;
            if (!TryPose(proposed, renderedDistance, aspect, out position)) return;
        }

        target = proposed;
        transform.SetPositionAndRotation(position, rotation);

        bool TryPose(Vector3 pivot, float offset, float candidateAspect, out Vector3 cameraPosition)
        {
            cameraPosition = pivot - rotation * Vector3.forward * offset;
            if (!InsideValley(pivot) || !InsideValley(cameraPosition) || !Inside(cameraPosition) ||
                cameraPosition.y < origin.y + terrain.SampleHeight(cameraPosition) + 8f)
                return false;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 ray = rotation * new Vector3(side * vertical * candidateAspect, -vertical, 1f);
                float depth = (origin.y - cameraPosition.y) / ray.y;
                if (depth <= view.nearClipPlane || !Inside(cameraPosition + ray * depth)) return false;
            }
            return true;
        }

        bool Inside(Vector3 point)
        {
            Vector3 local = point - origin;
            return local.x >= EdgeMargin && local.z >= EdgeMargin &&
                local.x <= data.size.x - EdgeMargin && local.z <= data.size.z - EdgeMargin;
        }
    }

    void BuildValley(TerrainData data, Vector3 center)
    {
        valleyData = data;
        valleyCenter = new Vector2(center.x, center.z);
        Vector3 origin = terrain.transform.position;
        float span = Mathf.Min(data.size.x, data.size.z);
        float baseline = Height(center);
        float fallback = Mathf.Min(playableHalfSize, span * .39f);
        const float step = 6f;
        for (int side = 0; side < ValleySides; side++)
        {
            float angle = side * Mathf.PI * 2f / ValleySides;
            Vector2 normal = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            valleyNormals[side] = normal;
            valleyLimits[side] = fallback;
            int uphill = 0;
            for (float radius = Mathf.Min(150f, span * .17f); radius <= fallback + 36f; radius += step)
            {
                float height = Height(center + new Vector3(normal.x * radius, 0f, normal.y * radius));
                uphill = height >= baseline + 8f ? uphill + 1 : 0;
                if (uphill < 3) continue;
                // Pull back from the first sustained rise, leaving the mountain foothills outside.
                valleyLimits[side] = Mathf.Min(fallback, radius - 2f * step - 22f);
                break;
            }
        }

        float Height(Vector3 point) => data.GetInterpolatedHeight(
            (point.x - origin.x) / data.size.x, (point.z - origin.z) / data.size.z);
    }

    bool InsideValley(Vector3 point)
    {
        Vector2 offset = new Vector2(point.x, point.z) - valleyCenter;
        for (int side = 0; side < ValleySides; side++)
        {
            if (Vector2.Dot(offset, valleyNormals[side]) > valleyLimits[side]) return false;
        }
        return true;
    }
}
