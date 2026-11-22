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
    [SerializeField] float minDistance = 90f;
    [SerializeField] float playableHalfSize = 290f;
    [SerializeField] Terrain terrain;

    Camera view;
    float zoom;
    float renderedDistance;
    Vector3 panGoal;

    void Awake()
    {
        view = GetComponent<Camera>();
        panGoal = target;
        UpdatePose(0f, true);
    }

    void LateUpdate()
    {
        zoom = Mathf.Clamp01(zoom - Input.mouseScrollDelta.y * .095f);
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
        float aspect = Mathf.Max(.001f, view.aspect);
        float vertical = Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad);
        Quaternion rotation = Quaternion.Euler(Pitch, yaw, 0f);
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
            // The bottom of the frame stays on the playable Terrain. The shallower
            // rays above it cross the surrounding terrain and reach the lunar sky.
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 ray = rotation * new Vector3(side * horizontal, -vertical, 1f);
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

    Vector3 FollowGround(Vector3 point)
    {
        point.y = GroundAt(point) + 12f;
        return point;
    }

    float GroundAt(Vector3 point) => terrain.transform.position.y + terrain.SampleHeight(point);
}
