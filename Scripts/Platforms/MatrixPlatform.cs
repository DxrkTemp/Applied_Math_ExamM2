using UnityEngine;

[DefaultExecutionOrder(-50)]
public class MatrixPlatform : MonoBehaviour
{
    public enum MoveMode { None, PingPong, Orbit }

    [Header("Translation (matrix T)")]
    public MoveMode moveMode = MoveMode.PingPong;
    [Tooltip("PingPong: how far the platform travels from its start (local space).")]
    public Vector3 moveOffset = new Vector3(0f, 0f, 4f);
    [Tooltip("Full back-and-forth trips per second.")]
    public float moveSpeed = 0.25f;
    [Tooltip("Orbit: radius of the circle around the start point.")]
    public float orbitRadius = 3f;
    [Tooltip("Orbit: degrees per second around the circle.")]
    public float orbitDegreesPerSecond = 30f;

    [Header("Rotation (matrix R)")]
    [Tooltip("Degrees per second on each axis. Keep X and Z at 0 for platforms you stand on.")]
    public Vector3 spinDegreesPerSecond = Vector3.zero;

    [Header("Scale (matrix S)")]
    public bool pulseScale = false;
    [Tooltip("How much bigger/smaller it gets, e.g. 0.3 = +/-30%.")]
    public float pulseAmount = 0.3f;
    public float pulseSpeed = 0.5f;

    [Header("Timing")]
    [Tooltip("Offset so several platforms don't move in sync.")]
    public float timeOffset = 0f;

    [Header("Debug")]
    public bool logMatrix = false;

    public Matrix4x4 FrameDelta { get; private set; } = Matrix4x4.identity;

    public Matrix4x4 CurrentMatrix { get; private set; } = Matrix4x4.identity;

    Vector3 basePosition;
    Vector3 baseEuler;
    Vector3 baseScale;
    Matrix4x4 lastWorldMatrix;

    void Awake()
    {
        basePosition = transform.localPosition;
        baseEuler = transform.localEulerAngles;
        baseScale = transform.localScale;
        lastWorldMatrix = transform.localToWorldMatrix;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.None;
    }

    void Update()
    {
        float t = Time.time + timeOffset;

        Vector3 offset = Vector3.zero;
        if (moveMode == MoveMode.PingPong)
        {
            float k = (Mathf.Sin(t * moveSpeed * 2f * Mathf.PI) + 1f) * 0.5f;
            offset = moveOffset * k;
        }
        else if (moveMode == MoveMode.Orbit)
        {
            Matrix4x4 orbitRot = MatrixMath.RotationY(t * orbitDegreesPerSecond);
            offset = orbitRot.MultiplyPoint3x4(new Vector3(orbitRadius, 0f, 0f));
        }

        Vector3 euler = baseEuler + spinDegreesPerSecond * t;

        float pulse = pulseScale ? 1f + Mathf.Sin(t * pulseSpeed * 2f * Mathf.PI) * pulseAmount : 1f;
        Vector3 scale = baseScale * pulse;

        Matrix4x4 T = MatrixMath.Translation(basePosition + offset);
        Matrix4x4 R = MatrixMath.RotationEuler(euler);
        Matrix4x4 S = MatrixMath.Scale(scale);
        CurrentMatrix = T * R * S;
        MatrixMath.ApplyToLocal(transform, CurrentMatrix);

        Matrix4x4 nowWorld = transform.localToWorldMatrix;
        FrameDelta = nowWorld * lastWorldMatrix.inverse;
        lastWorldMatrix = nowWorld;

        if (logMatrix) Debug.Log(name + " M =\n" + CurrentMatrix);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 start = Application.isPlaying && transform.parent != null
            ? transform.parent.TransformPoint(basePosition)
            : (Application.isPlaying ? basePosition : transform.position);

        if (moveMode == MoveMode.PingPong)
        {
            Vector3 end = transform.parent != null
                ? start + transform.parent.TransformVector(moveOffset)
                : start + moveOffset;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawWireSphere(end, 0.3f);
        }
        else if (moveMode == MoveMode.Orbit)
        {
            const int steps = 32;
            Vector3 prev = start + new Vector3(orbitRadius, 0f, 0f);
            for (int i = 1; i <= steps; i++)
            {
                Vector3 p = start + MatrixMath.RotationY(360f * i / steps).MultiplyPoint3x4(new Vector3(orbitRadius, 0f, 0f));
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
    }
}
