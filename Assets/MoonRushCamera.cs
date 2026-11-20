using UnityEngine;

public class MoonRushCamera : MonoBehaviour
{
    public Vector3 target = new Vector3(0f, 0f, 0f);
    public float distance = 210f;
    public float yaw = 35f;
    public float pitch = 52f;
    public float minDistance = 45f;
    public float maxDistance = 300f;
    public float moveSpeed = 0.65f;
    public float rotateSpeed = 3f;
    public float terrainHalfSize = 450f;
    public float playableHalfSize = 265f;

    void LateUpdate()
    {
        float zoom = Input.mouseScrollDelta.y;
        distance = Mathf.Clamp(distance * Mathf.Exp(-zoom * 0.12f), minDistance, maxDistance);
        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * rotateSpeed;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * rotateSpeed, 30f, 78f);
        }
        Vector3 forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        Vector3 right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 movement = (right * horizontal + forward * vertical) * moveSpeed * distance * Time.deltaTime;
        target += movement;
        target.x = Mathf.Clamp(target.x, -playableHalfSize, playableHalfSize);
        target.z = Mathf.Clamp(target.z, -playableHalfSize, playableHalfSize);
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.rotation = rotation;
        transform.position = target - rotation * Vector3.forward * distance;
    }
}
