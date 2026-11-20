using UnityEngine;

public class MoonRushCamera : MonoBehaviour
{
    public Vector3 target = new Vector3(0f, 0f, 0f);
    public float distance = 315f;
    public float yaw = 35f;
    public float pitch = 54f;
    public float minDistance = 45f;
    public float maxDistance = 330f;
    public float moveSpeed = 0.65f;
    public float rotateSpeed = 3f;
    public float terrainHalfSize = 450f;
    public float playableHalfSize = 245f;

    [SerializeField] Terrain terrain;

    Camera view;

    void Awake()
    {
        view = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * rotateSpeed;
            pitch -= Input.GetAxis("Mouse Y") * rotateSpeed;
        }
        pitch = Mathf.Clamp(pitch, 52f, 78f);

        Vector3 center = Vector3.zero;
        TerrainData data = terrain != null ? terrain.terrainData : null;
        float halfSize = terrainHalfSize;
        if (data != null)
        {
            center = terrain.transform.position + data.size * .5f;
            halfSize = Mathf.Min(data.size.x, data.size.z) * .5f;
        }

        distance = Mathf.Clamp(distance * Mathf.Exp(-Input.mouseScrollDelta.y * .12f), minDistance, maxDistance);

        Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
        var input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        target += heading * Vector3.ClampMagnitude(input, 1f) * (moveSpeed * distance * Time.deltaTime);
        float panLimit = Mathf.Min(playableHalfSize, halfSize * .43f);
        target.x = Mathf.Clamp(target.x, center.x - panLimit, center.x + panLimit);
        target.z = Mathf.Clamp(target.z, center.z - panLimit, center.z + panLimit);
        if (data != null)
        {
            float ground = terrain.transform.position.y + terrain.SampleHeight(target);
            target.y = Mathf.Lerp(target.y, ground + 2f, 1f - Mathf.Exp(-9f * Time.deltaTime));
        }

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        float visibleDistance = data != null && view != null
            ? LimitDistance(rotation, terrain.transform.position, data.size)
            : distance;
        transform.SetPositionAndRotation(target - rotation * Vector3.forward * visibleDistance, rotation);
    }

    float LimitDistance(Quaternion rotation, Vector3 origin, Vector3 size)
    {
        // Intersect the four view rays with the Terrain's lowest possible plane.
        // If that entire footprint fits, a raised part of the Terrain occludes the boundary.
        if (FitsTerrain(distance, rotation, origin, size)) return distance;

        float lower = 0f;
        float upper = distance;
        for (int step = 0; step < 9; step++)
        {
            float candidate = (lower + upper) * .5f;
            if (FitsTerrain(candidate, rotation, origin, size)) lower = candidate;
            else upper = candidate;
        }
        return lower;
    }

    bool FitsTerrain(float candidate, Quaternion rotation, Vector3 origin, Vector3 size)
    {
        Vector3 cameraPosition = target - rotation * Vector3.forward * candidate;
        float vertical = Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad);
        float horizontal = vertical * view.aspect;
        for (int corner = 0; corner < 4; corner++)
        {
            Vector3 ray = rotation * new Vector3(
                (corner & 1) == 0 ? -horizontal : horizontal,
                (corner & 2) == 0 ? -vertical : vertical, 1f);
            if (ray.y >= 0f) return false;
            Vector3 hit = cameraPosition + ray * ((origin.y - cameraPosition.y) / ray.y) - origin;
            const float margin = 12f;
            if (hit.x < margin || hit.x > size.x - margin ||
                hit.z < margin || hit.z > size.z - margin) return false;
        }
        return true;
    }
}
