using UnityEngine;

[RequireComponent(typeof(Camera))]
public class MoonRushCamera : MonoBehaviour
{
    [SerializeField] Vector3 target = new Vector3(0f, 23f, 0f);
    [SerializeField] float distance = 900f;
    [SerializeField] float startDistance = 520f;
    [SerializeField] float yaw = 35f;
    [SerializeField, Range(55f, 65f)] float pitch = 56f;
    [SerializeField] float minDistance = 90f;
    [SerializeField] float playableHalfSize = 290f;
    [SerializeField] Terrain terrain;

    Camera view;
    float baseFov;
    float zoom;
    float renderedDistance;
    Vector3 panGoal;
    Vector2 lastPointer;
    int pointerCount;
    float lastSpan;
    int fingerA = -1;
    int fingerB = -1;

    void Awake()
    {
        view = GetComponent<Camera>();
        baseFov = view.fieldOfView;
        panGoal = target;
        UpdatePose(0f, true);
    }

    void LateUpdate()
    {
        Vector2 drag = ReadPointer(out float zoomDelta);
        zoom = Mathf.Clamp01(zoom - zoomDelta * .095f);

        Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
        float unitsPerPixel = 2f * renderedDistance * Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad)
            / Mathf.Max(1, view.pixelHeight);
        panGoal -= heading * new Vector3(drag.x, 0f, drag.y / Mathf.Sin(pitch * Mathf.Deg2Rad)) * unitsPerPixel;
        var keys = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        panGoal += heading * Vector3.ClampMagnitude(keys, 1f) * (renderedDistance * .55f * Time.unscaledDeltaTime);
        UpdatePose(Time.unscaledDeltaTime);
    }

    Vector2 ReadPointer(out float zoomDelta)
    {
        int count = Mathf.Min(Input.touchCount, 2);
        bool touch = count > 0;
        if (!touch && Input.GetMouseButton(0)) count = 1;
        Vector2 pointer = Input.mousePosition;
        float span = 0f;
        int first = -1;
        int second = -1;
        zoomDelta = touch ? 0f : Input.mouseScrollDelta.y;
        if (touch)
        {
            Touch a = Input.GetTouch(0);
            first = a.fingerId;
            pointer = a.position;
            if (count == 2)
            {
                Touch b = Input.GetTouch(1);
                second = b.fingerId;
                span = Vector2.Distance(a.position, b.position);
                pointer = (a.position + b.position) * .5f;
            }
        }

        Vector2 delta = Vector2.zero;
        if (count > 0 && count == pointerCount && first == fingerA && second == fingerB)
        {
            delta = pointer - lastPointer;
            if (count == 2 && lastSpan > 1f)
                zoomDelta = Mathf.Log(Mathf.Max(1f, span) / lastSpan) * 8f;
        }
        pointerCount = count;
        lastPointer = pointer;
        lastSpan = span;
        fingerA = first;
        fingerB = second;
        return delta;
    }

    void UpdatePose(float dt, bool initialize = false)
    {
        float blend = dt > 0f ? 1f - Mathf.Exp(-13f * dt) : 1f;
        // Cap horizontal FOV on ultrawide screens while preserving the normal vertical composition.
        float tangent = Mathf.Min(Mathf.Tan(baseFov * .5f * Mathf.Deg2Rad),
            Mathf.Tan(44f * Mathf.Deg2Rad) / Mathf.Max(.01f, view.aspect));
        view.fieldOfView = 2f * Mathf.Atan(tangent) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        TerrainData data = terrain != null ? terrain.terrainData : null;
        float limit = distance;

        if (data != null)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            Vector3 center = origin + size * .5f;
            float panLimit = Mathf.Min(playableHalfSize, Mathf.Min(size.x, size.z) * .33f);
            panGoal.x = Mathf.Clamp(panGoal.x, center.x - panLimit, center.x + panLimit);
            panGoal.z = Mathf.Clamp(panGoal.z, center.z - panLimit, center.z + panLimit);
            target = Vector3.Lerp(target, panGoal, blend);
            target.y = GroundAt(target) + 12f;

            limit = ClampToTerrain(limit, rotation, origin, size, tangent);
        }
        else
        {
            target = Vector3.Lerp(target, panGoal, blend);
        }

        float nearest = Mathf.Min(minDistance, limit);
        if (initialize)
            zoom = limit > nearest ? Mathf.InverseLerp(nearest, limit, startDistance) : 1f;
        float wanted = Mathf.Lerp(nearest, limit, zoom);
        renderedDistance = Mathf.Min(limit, Mathf.Lerp(renderedDistance, wanted, blend));
        if (data != null)
            renderedDistance = ClampToTerrain(renderedDistance, rotation, terrain.transform.position, data.size, tangent);
        Vector3 position = target - rotation * Vector3.forward * renderedDistance;
        if (data != null) position.y = Mathf.Max(position.y, GroundAt(position) + 8f);
        transform.SetPositionAndRotation(position, rotation);
    }

    float ClampToTerrain(float requested, Quaternion rotation, Vector3 origin, Vector3 size, float tangent)
    {
        if (FitsTerrain(requested, rotation, origin, size, tangent)) return requested;
        float low = 0f;
        float high = requested;
        for (int step = 0; step < 12; step++)
        {
            float candidate = (low + high) * .5f;
            if (FitsTerrain(candidate, rotation, origin, size, tangent)) low = candidate;
            else high = candidate;
        }
        return low;
    }

    float GroundAt(Vector3 point)
    {
        return terrain.transform.position.y + terrain.SampleHeight(point);
    }

    bool FitsTerrain(float candidate, Quaternion rotation, Vector3 origin, Vector3 size, float vertical)
    {
        Vector3 position = target - rotation * Vector3.forward * candidate;
        position.y = Mathf.Max(position.y, GroundAt(position) + 8f);
        float horizontal = vertical * view.aspect;
        for (int corner = 0; corner < 4; corner++)
        {
            Vector3 ray = rotation * new Vector3((corner & 1) == 0 ? -horizontal : horizontal,
                (corner & 2) == 0 ? -vertical : vertical, 1f);
            if (ray.y >= 0f) return false;
            Vector3 hit = position + ray * ((origin.y - position.y) / ray.y) - origin;
            const float margin = 16f;
            if (hit.x < margin || hit.x > size.x - margin || hit.z < margin || hit.z > size.z - margin)
                return false;
        }
        return true;
    }
}
