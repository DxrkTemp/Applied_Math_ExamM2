using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement (WASD)")]
    public float moveSpeed = 5f;
    public float sprintMultiplier = 1.6f;
    [Tooltip("How fast the character turns to face the move direction.")]
    public float turnSpeed = 12f;
    [Tooltip("Camera used to make WASD camera-relative. Leaves empty = Main Camera.")]
    public Transform cameraTransform;

    [Header("Jump + Manual Gravity")]
    [Tooltip("Our own gravity in m/s^2 (negative = down).")]
    public float gravity = -20f;
    [Tooltip("How high one jump goes in metres.")]
    public float jumpHeight = 2.2f;
    [Tooltip("Fall faster than you rise, feels less floaty.")]
    public float fallMultiplier = 1.5f;
    public float terminalVelocity = -40f;
    [Tooltip("Small grace period after walking off a ledge where jump still works.")]
    public float coyoteTime = 0.1f;
    [Tooltip("Pressing jump slightly before landing still counts.")]
    public float jumpBufferTime = 0.12f;

    [Header("Landing / Ground Check")]
    public LayerMask groundMask = ~0;
    public float groundCheckDistance = 0.15f;

    public Vector3 CurrentPosition { get; private set; }
    public Vector3 NewPosition { get; private set; }
    public Vector3 Velocity => velocity;
    public bool IsGrounded { get; private set; }
    public bool InputEnabled { get; set; } = true;
    public MatrixPlatform CurrentPlatform => currentPlatform;

    public System.Action OnJumped;
    public System.Action OnLanded;

    CharacterController controller;
    Vector3 velocity;
    float lastGroundedTime = -999f;
    float lastJumpPressedTime = -999f;
    bool hasJumped;
    bool wasGrounded;
    MatrixPlatform currentPlatform;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        CurrentPosition = NewPosition = transform.position;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (IsGrounded && currentPlatform != null)
        {
            Matrix4x4 delta = currentPlatform.FrameDelta;
            Vector3 p = transform.position;
            Vector3 carried = delta.MultiplyPoint3x4(p);
            controller.Move(carried - p);

            Vector3 f = delta.MultiplyVector(transform.forward);
            f.y = 0f;
            if (f.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
        }

        GroundCheck();
        if (IsGrounded)
        {
            lastGroundedTime = Time.time;
            if (velocity.y <= 0f) hasJumped = false;
            if (velocity.y < 0f) velocity.y = -2f;
        }
        if (IsGrounded && !wasGrounded) OnLanded?.Invoke();
        wasGrounded = IsGrounded;

        Vector2 input = InputEnabled ? GameInput.Move : Vector2.zero;
        Vector3 camForward = Vector3.forward, camRight = Vector3.right;
        if (cameraTransform != null)
        {
            camForward = cameraTransform.forward; camForward.y = 0f; camForward.Normalize();
            camRight = cameraTransform.right;     camRight.y = 0f;   camRight.Normalize();
        }
        Vector3 moveDir = camForward * input.y + camRight * input.x;
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        float speed = moveSpeed * (InputEnabled && GameInput.Sprint ? sprintMultiplier : 1f);
        Vector3 horizontal = moveDir * speed;
        velocity.x = horizontal.x;
        velocity.z = horizontal.z;

        if (moveDir.sqrMagnitude > 0.001f)
        {
            Quaternion target = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, turnSpeed * dt);
        }

        if (InputEnabled && GameInput.JumpPressed) lastJumpPressedTime = Time.time;

        bool jumpBuffered = Time.time - lastJumpPressedTime <= jumpBufferTime;
        bool canJump = !hasJumped && (Time.time - lastGroundedTime <= coyoteTime);
        if (jumpBuffered && canJump)
        {
            velocity.y = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
            hasJumped = true;
            IsGrounded = false;
            lastJumpPressedTime = -999f;
            currentPlatform = null;
            OnJumped?.Invoke();
        }

        if (!IsGrounded || velocity.y > 0f)
        {
            float g = velocity.y < 0f ? gravity * fallMultiplier : gravity;
            velocity.y += g * dt;
            if (velocity.y < terminalVelocity) velocity.y = terminalVelocity;
        }

        Vector3 displacement = velocity * dt;
        CurrentPosition = transform.position;
        NewPosition = CurrentPosition + displacement;
        CollisionFlags flags = controller.Move(displacement);

        if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;
    }

    void GroundCheck()
    {
        float radius = controller.radius * 0.95f;
        Vector3 centre = transform.TransformPoint(controller.center);
        float castDistance = controller.height * 0.5f - radius + groundCheckDistance + controller.skinWidth;

        bool hitGround = Physics.SphereCast(centre, radius, Vector3.down, out RaycastHit hit,
                                            castDistance, groundMask, QueryTriggerInteraction.Ignore);

        bool rising = velocity.y > 0.1f;
        IsGrounded = !rising && (hitGround || controller.isGrounded);

        currentPlatform = (IsGrounded && hitGround) ? hit.collider.GetComponentInParent<MatrixPlatform>() : null;
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        controller.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        controller.enabled = true;
        velocity = Vector3.zero;
        hasJumped = false;
        currentPlatform = null;
        CurrentPosition = NewPosition = position;
    }

    void OnDrawGizmosSelected()
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc == null) return;
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Vector3 feet = transform.TransformPoint(cc.center) + Vector3.down * (cc.height * 0.5f);
        Gizmos.DrawWireSphere(feet, cc.radius * 0.95f);
    }
}
