using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    [Tooltip("Height of the look-at point above the player's feet.")]
    public float pivotHeight = 1.5f;
    public float distance = 5f;
    public float minPitch = -20f;
    public float maxPitch = 65f;
    public float mouseSensitivity = 2.5f;
    [Tooltip("How quickly the camera follows. Higher = snappier.")]
    public float followSharpness = 15f;

    [Header("Wall collision")]
    public LayerMask collisionMask = ~0;
    public float collisionRadius = 0.25f;

    [Header("Start angles")]
    public float yaw = 0f;
    public float pitch = 15f;

    public bool lockCursor = true;

    public Matrix4x4 RotationMatrix { get; private set; } = Matrix4x4.identity;

    void Start()
    {
        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }
        if (target != null) yaw = target.eulerAngles.y;
        SetCursorLocked(lockCursor);
    }

    void LateUpdate()
    {
        if (GameInput.PausePressed) SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);
        if (target == null) return;

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 look = GameInput.Look;
            yaw += look.x * mouseSensitivity;
            pitch -= look.y * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        RotationMatrix = MatrixMath.RotationY(yaw) * MatrixMath.RotationX(pitch);
        Vector3 offset = RotationMatrix.MultiplyVector(new Vector3(0f, 0f, -distance));
        Vector3 pivot = target.position + Vector3.up * pivotHeight;
        Vector3 desired = pivot + offset;

        Vector3 dir = desired - pivot;
        float len = dir.magnitude;
        if (len > 0.001f && Physics.SphereCast(pivot, collisionRadius, dir / len, out RaycastHit hit,
                                               len, collisionMask, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.transform.IsChildOf(target))
                desired = pivot + dir / len * Mathf.Max(0.5f, hit.distance);
        }

        transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
        transform.rotation = Quaternion.LookRotation(pivot - transform.position, Vector3.up);
    }

    public static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
