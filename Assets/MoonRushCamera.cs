using UnityEngine;

[RequireComponent(typeof(Camera))]
public class MoonRushCamera : MonoBehaviour
{
    [SerializeField] Vector3 target = new Vector3(0f, 23f, 0f);
    [SerializeField] float distance = 900f;
    [SerializeField] float startDistance = 520f;
    [SerializeField] float yaw = 35f;
    [SerializeField, Range(32f, 88f)] float pitch = 57f;
    const float MinPitch = 32f;
    const float MaxPitch = 88f;
    const float EdgeMargin = 16f;
    [SerializeField] float minDistance = 90f;
    [SerializeField] float playableHalfSize = 290f;
    [SerializeField] Terrain terrain;

    Camera view;
    float baseFov;
    float zoom;
    float renderedDistance;
    float pitchGoal;
    float renderedPitch;
    float lastTiltY;
    bool tilting;
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
        renderedPitch = pitchGoal = Mathf.Clamp(pitch, MinPitch, MaxPitch);
        UpdatePose(0f, true);
    }

    void LateUpdate()
    {
        bool tiltHeld = Input.touchCount == 0 && Input.GetMouseButton(1);
        float pointerY = Input.mousePosition.y;
        if (tiltHeld && tilting)
            pitchGoal = Mathf.Clamp(pitchGoal + (pointerY - lastTiltY) * .16f, MinPitch, MaxPitch);
        lastTiltY = pointerY;
        tilting = tiltHeld;

        Vector2 drag = ReadPointer(out float zoomDelta);
        zoom = Mathf.Clamp01(zoom - zoomDelta * .095f);

        Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
        float unitsPerPixel = 2f * renderedDistance * Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad)
            / Mathf.Max(1, view.pixelHeight);
        panGoal -= heading * new Vector3(drag.x, 0f, drag.y / Mathf.Sin(renderedPitch * Mathf.Deg2Rad)) * unitsPerPixel;
        var keys = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        panGoal += heading * Vector3.ClampMagnitude(keys, 1f) * (renderedDistance * .55f * Time.unscaledDeltaTime);
        UpdatePose(Time.unscaledDeltaTime);
    }

    Vector2 ReadPointer(out float zoomDelta)
    {
        int count = Mathf.Min(Input.touchCount, 2);
        bool touch = count > 0;
        if (!touch && !tilting && Input.GetMouseButton(0)) count = 1;
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
        renderedPitch = Mathf.Lerp(renderedPitch, pitchGoal, blend);
        // Keep upper rays pitched into the ground and cap horizontal FOV on ultrawide screens.
        float aspect = Mathf.Max(.001f, view.aspect);
        float vertical = Mathf.Tan(Mathf.Min(baseFov * .5f, renderedPitch - 18f) * Mathf.Deg2Rad);
        vertical = Mathf.Min(vertical, Mathf.Tan(44f * Mathf.Deg2Rad) / aspect);
        view.fieldOfView = 2f * Mathf.Atan(vertical) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(renderedPitch, yaw, 0f);
        TerrainData data = terrain != null ? terrain.terrainData : null;
        if (data == null)
        {
            target = Vector3.Lerp(target, panGoal, blend);
            renderedDistance = Mathf.Lerp(renderedDistance,
                initialize ? startDistance : Mathf.Lerp(minDistance, distance, zoom), blend);
            if (initialize) zoom = Mathf.InverseLerp(minDistance, distance, startDistance);
            transform.SetPositionAndRotation(target - rotation * Vector3.forward * renderedDistance, rotation);
            return;
        }

        Vector3 origin = terrain.transform.position;
        Vector3 center = origin + data.size * .5f;
        float panLimit = Mathf.Min(playableHalfSize, Mathf.Min(data.size.x, data.size.z) * .33f);
        panGoal.x = Mathf.Clamp(panGoal.x, center.x - panLimit, center.x + panLimit);
        panGoal.z = Mathf.Clamp(panGoal.z, center.z - panLimit, center.z + panLimit);

        // Check the full frustum at the proposed pan, zoom and pitch, not just its target.
        target = FollowGround(Vector3.Lerp(target, panGoal, blend));
        float nearest = Mathf.Min(minDistance, distance);
        if (!TryPose(target, nearest, out _))
        {
            Vector3 centered = FollowGround(center);
            if (!TryPose(centered, nearest, out _)) nearest = 0f;

            Vector3 proposed = target;
            float low = 0f;
            float high = 1f;
            for (int step = 0; step < 12; step++)
            {
                float t = (low + high) * .5f;
                if (TryPose(FollowGround(Vector3.Lerp(centered, proposed, t)), nearest, out _)) low = t;
                else high = t;
            }
            target = FollowGround(Vector3.Lerp(centered, proposed, low));
        }

        float limit = LimitDistance(nearest, distance);
        if (initialize) zoom = Mathf.InverseLerp(nearest, limit, startDistance);
        float wanted = Mathf.Lerp(nearest, limit, zoom);
        renderedDistance = LimitDistance(nearest,
            Mathf.Clamp(Mathf.Lerp(renderedDistance, wanted, blend), nearest, limit));
        if (TryPose(target, renderedDistance, out Vector3 position))
            transform.SetPositionAndRotation(position, rotation);

        float LimitDistance(float safe, float requested)
        {
            if (TryPose(target, requested, out _)) return requested;
            for (int step = 0; step < 12; step++)
            {
                float candidate = (safe + requested) * .5f;
                if (TryPose(target, candidate, out _)) safe = candidate;
                else requested = candidate;
            }
            return safe;
        }

        bool TryPose(Vector3 pivot, float offset, out Vector3 cameraPosition)
        {
            cameraPosition = pivot - rotation * Vector3.forward * offset;
            if (!Inside(cameraPosition)) return false;
            cameraPosition.y = Mathf.Max(cameraPosition.y, GroundAt(cameraPosition) + 8f);
            float horizontal = vertical * aspect;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector3 ray = rotation * new Vector3((corner & 1) == 0 ? -horizontal : horizontal,
                    (corner & 2) == 0 ? -vertical : vertical, 1f);
                if (ray.y >= 0f) return false;
                float depth = (origin.y - cameraPosition.y) / ray.y;
                // depth is camera-space Z because the input ray has a Z component of one.
                if (depth <= view.nearClipPlane || depth >= view.farClipPlane ||
                    !Inside(cameraPosition + ray * depth)) return false;
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

    Vector3 FollowGround(Vector3 point)
    {
        point.y = GroundAt(point) + 12f;
        return point;
    }

    float GroundAt(Vector3 point) => terrain.transform.position.y + terrain.SampleHeight(point);
}
