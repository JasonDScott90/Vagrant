using UnityEngine;


[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Speeds (m/s)")]
    public float crouchSpeed = 1.7f;
    public float runSpeed = 5.5f;      // default speed
    public float sprintSpeed = 7.5f;   // hold sprint
    public float backwardMultiplier = 0.9f;
    public float strafeMultiplier = 0.95f;

    [Header("Feel")]
    public float groundAcceleration = 40f;
    public float groundDeceleration = 50f;
    [Range(0f, 1f)] public float airControl = 0.05f; 

    [Header("Jump / Gravity")]
    public float gravityForce = 30f;   // positive number (renamed so old saved values are ignored)
    public float jumpHeightMeters = 0.9f;
    public float fallMultiplier = 1.5f; // extra gravity while falling
    public float maxFallSpeed = 50f;
    public float jumpBufferTime = 0.1f;
    public float coyoteTime = 0.08f;

    [Header("Crouch")]
    public float crouchHeight = 1.2f;
    public float crouchTransitionSpeed = 8f;
    public Transform cameraTransform;  // auto-found if empty

    [Header("Slopes")]
    public float slopeSlideSpeed = 6f;
    public LayerMask groundMask = ~0;

    CharacterController controller;
    Vector3 velocity;                  // x/z = horizontal momentum, y = vertical
    float standHeight;
    Vector3 standCenter;
    float camStandY;
    float lastGroundedTime = -10f;
    float lastJumpPressTime = -10f;
    bool crouching;
    bool grounded;
    Vector3 groundNormal = Vector3.up;

    public bool IsGrounded => grounded;
    public bool IsCrouching => crouching;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        standHeight = controller.height;
        standCenter = controller.center;

        if (cameraTransform == null)
        {
            Camera c = GetComponentInChildren<Camera>();
            if (c != null) cameraTransform = c.transform;
        }
        if (cameraTransform != null) camStandY = cameraTransform.localPosition.y;
    }

    // Called every frame by InputManager.
    public void ProcessMove(Vector2 input, bool sprint, bool crouchHeld)
    {
        input = Vector2.ClampMagnitude(input, 1f);
        UpdateGrounded();
        UpdateCrouch(crouchHeld);

        // --- target speed ---
        bool sprinting = sprint && input.y > 0.1f && !crouching;
        float speed = runSpeed;
        if (crouching) speed = crouchSpeed;
        else if (sprinting) speed = sprintSpeed;

        if (input.y < -0.1f) speed *= backwardMultiplier;
        else if (Mathf.Abs(input.x) > 0.1f && Mathf.Abs(input.y) < 0.1f) speed *= strafeMultiplier;

        Vector3 wishDir = transform.right * input.x + transform.forward * input.y;
        if (grounded && wishDir.sqrMagnitude > 0f)
            wishDir = Vector3.ProjectOnPlane(wishDir, groundNormal).normalized * wishDir.magnitude;
        Vector3 target = wishDir * speed;
        target.y = 0f;

        // --- horizontal momentum ---
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        float rate = grounded
            ? (target.sqrMagnitude > 0.01f ? groundAcceleration : groundDeceleration)
            : groundAcceleration * airControl;
        horizontal = Vector3.MoveTowards(horizontal, target, rate * Time.deltaTime);

        // --- jump (buffered + coyote time, not while crouched) ---
        if ((Time.time - lastJumpPressTime) <= jumpBufferTime &&
            (Time.time - lastGroundedTime) <= coyoteTime && !crouching)
        {
            velocity.y = Mathf.Sqrt(2f * Mathf.Abs(gravityForce) * jumpHeightMeters);
            lastJumpPressTime = -10f;
            lastGroundedTime = -10f;
            grounded = false;
        }

        // --- gravity ---
        if (grounded && velocity.y < 0f) velocity.y = -2f;
        else
        {
            // Fall faster than you rise: this is what makes jumps feel weighty instead of floaty.
            float g = Mathf.Abs(gravityForce) * (velocity.y < 0f ? fallMultiplier : 1f);
            velocity.y = Mathf.Max(velocity.y - g * Time.deltaTime, -maxFallSpeed);
        }

        // --- slide down steep slopes ---
        Vector3 slide = Vector3.zero;
        if (grounded && Vector3.Angle(Vector3.up, groundNormal) > controller.slopeLimit)
            slide = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized * slopeSlideSpeed;

        velocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
        controller.Move((velocity + slide) * Time.deltaTime);

        // Hitting a wall kills that momentum so you don't "store" speed against it.
        if ((controller.collisionFlags & CollisionFlags.Sides) != 0)
        {
            velocity.x = controller.velocity.x;
            velocity.z = controller.velocity.z;
        }
        if ((controller.collisionFlags & CollisionFlags.Above) != 0 && velocity.y > 0f)
            velocity.y = 0f;
    }

    public void Jump()
    {
        lastJumpPressTime = Time.time;
    }

    // World position of the bottom of the capsule.
    Vector3 FeetPosition()
    {
        return transform.position + controller.center - Vector3.up * (controller.height * 0.5f);
    }

    void UpdateGrounded()
    {
        grounded = controller.isGrounded;
        groundNormal = Vector3.up;

        Vector3 feet = FeetPosition();
        float r = controller.radius * 0.9f;
        if (Physics.SphereCast(feet + Vector3.up * (r + 0.1f), r, Vector3.down, out RaycastHit hit,
                0.35f, groundMask, QueryTriggerInteraction.Ignore))
        {
            groundNormal = hit.normal;
            if (velocity.y <= 0f && hit.distance < 0.2f) grounded = true;
        }

        if (grounded) lastGroundedTime = Time.time;
    }

    void UpdateCrouch(bool crouchHeld)
    {
        crouching = crouchHeld;

        // Don't stand up if there's something above us.
        if (!crouching && controller.height < standHeight - 0.01f)
        {
            Vector3 feet = FeetPosition();
            float r = controller.radius * 0.95f;
            Vector3 origin = feet + Vector3.up * (controller.height - r);
            float needed = standHeight - controller.height;
            if (Physics.SphereCast(origin, r, Vector3.up, out _, needed + 0.05f, groundMask,
                    QueryTriggerInteraction.Ignore))
                crouching = true;
        }

        float targetHeight = crouching ? crouchHeight : standHeight;
        if (Mathf.Approximately(controller.height, targetHeight)) return;

        float newHeight = Mathf.MoveTowards(controller.height, targetHeight,
            crouchTransitionSpeed * Time.deltaTime);

        // Keep the feet planted: shrink/grow the capsule from the top.
        float bottom = standCenter.y - standHeight * 0.5f;
        controller.height = newHeight;
        controller.center = new Vector3(standCenter.x, bottom + newHeight * 0.5f, standCenter.z);

        if (cameraTransform != null)
        {
            Vector3 p = cameraTransform.localPosition;
            p.y = camStandY - (standHeight - newHeight);
            cameraTransform.localPosition = p;
        }
    }
}
