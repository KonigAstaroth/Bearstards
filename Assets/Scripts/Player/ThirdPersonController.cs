using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;

[RequireComponent(typeof(Rigidbody))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;

    [Header("Camera")]
    public Transform cam;
    public float mouseSensitivity = 0.12f;
    public float cameraDistance = 1f;
    public float cameraHeight = 1.3f;
    [SerializeField] private float cameraSideOffset = 0.8f;

    [Header("Physics")]
    [SerializeField] private float fallMultiplier = 2.5f;
    [SerializeField] private float groundCheckDistance = 1f;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckExtraDistance = 0.15f;

    [Header ("Sprint")]
    [SerializeField] private float sprintSpeed = 9f;
    private bool isRunning;

    private Collider playerCollider;

    private Rigidbody rb;
    private PhotonView view;
    private PlayerStateMachine stateMachine;

    private float yaw;
    private float pitch;

    private Vector3 moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();
        view = GetComponent<PhotonView>();
        stateMachine = GetComponent<PlayerStateMachine>();

        rb.freezeRotation = true;

        if (stateMachine == null)
        {
            Debug.LogError(
                "Falta PlayerStateMachine en el jugador.",
                this
            );
        }

        if (view != null && !view.IsMine)
        {
            if (cam != null)
                cam.gameObject.SetActive(false);

            enabled = false;
            return;
        }

        if (cam != null)
            cam.gameObject.SetActive(true);

        yaw = transform.eulerAngles.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (view == null || !view.IsMine)
            return;

        ReadMovementInput();
        Look();
    }

    private void FixedUpdate()
    {
        if (view == null || !view.IsMine)
            return;

        Move();
        ApplyFallGravity();
    }

    private void LateUpdate()
    {
        if (view == null || !view.IsMine || cam == null)
            return;

        PositionCamera();
    }

    public bool IsRunning()
    {
        return isRunning;
    }

    private void ReadMovementInput()
    {
        moveInput = Vector3.zero;

        if (Keyboard.current == null)
            return;

        var kb = Keyboard.current;

        if (kb.wKey.isPressed) moveInput.z += 1f;
        if (kb.sKey.isPressed) moveInput.z -= 1f;
        if (kb.dKey.isPressed) moveInput.x += 1f;
        if (kb.aKey.isPressed) moveInput.x -= 1f;

        moveInput = Vector3.ClampMagnitude(moveInput, 1f);

        bool shiftPressed = kb.leftShiftKey.isPressed;

        isRunning = shiftPressed && moveInput.magnitude > 0f;
    }

    public float GetMovementInputMagnitude()
    {
        return moveInput.magnitude;
    }

    public bool IsGrounded()
    {
        if (playerCollider == null)
            return false;

        Bounds bounds = playerCollider.bounds;
        Vector3 origin = bounds.center;
        float distance = bounds.extents.y + groundCheckExtraDistance;

        bool grounded = Physics.Raycast(
            origin,
            Vector3.down,
            distance
        );

        Debug.DrawRay(
            origin,
            Vector3.down * distance,
            grounded ? Color.green : Color.red
        );

        return grounded;
    }

    public void ApplyJumpImpulse(float force)
    {
        rb.AddForce(Vector3.up * force, ForceMode.Impulse);
    }

    private void Move()
    {
        Vector3 moveDirection = transform.forward * moveInput.z + transform.right * moveInput.x;

        float currentSpeed = isRunning ? sprintSpeed: moveSpeed;
        Vector3 velocity = moveDirection * currentSpeed;
        velocity.y = rb.linearVelocity.y;

        rb.linearVelocity = velocity;
    }

    private void ApplyFallGravity()
    {
        if (rb.linearVelocity.y < 0f)
        {
            rb.AddForce(
                Physics.gravity * (fallMultiplier - 1f),
                ForceMode.Acceleration
            );
        }
    }

    private void Look()
    {
        if (Mouse.current == null)
            return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        yaw += mouseDelta.x * mouseSensitivity;
        pitch -= mouseDelta.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -40f, 70f);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void PositionCamera()
    {
        Quaternion camRotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 focusPoint =
            transform.position + Vector3.up * cameraHeight;

        cam.position =
            focusPoint
            - camRotation * Vector3.forward * cameraDistance
            + camRotation * Vector3.right * cameraSideOffset;

        cam.rotation = camRotation;
    }
}