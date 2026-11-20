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

        // Extra-wide views and the smaller original heightfield need more margin.
        float aspectFactor = view != null ? Mathf.Min(1f, 2.05f / view.aspect) : 1f;
        float zoomLimit = Mathf.Max(minDistance, Mathf.Min(maxDistance, halfSize * .42f) * aspectFactor);
        distance = Mathf.Clamp(distance * Mathf.Exp(-Input.mouseScrollDelta.y * .12f), minDistance, zoomLimit);

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
        transform.SetPositionAndRotation(target - rotation * Vector3.forward * distance, rotation);
    }
}
