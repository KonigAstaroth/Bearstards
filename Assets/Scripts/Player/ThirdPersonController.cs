using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;

[RequireComponent(typeof(Rigidbody))]
public class ThirdPersonController : MonoBehaviour
{
    public Transform cam;
    public float moveSpeed = 6f;
    public float jumpForce = 5f;

    public float mouseSensitivity = 0.12f;
    public float cameraDistance = 1f;
    public float cameraHeight = 1.3f;

    private float cameraSideOffset = 0.8f;

    [SerializeField] private float fallMultiplier = 2.5f;
    [SerializeField] private Animator animator;

    PhotonView view;

    private Rigidbody rb;
    private float yaw;
    private float pitch;

    private void Start()
    {
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        view = GetComponent<PhotonView>();

        if (animator == null)
        {
            Debug.LogError("No asignaste el Animator en ThirdPersonController", this);
        }
        else
        {
            Debug.Log(
                $"Animator encontrado: {animator.gameObject.name} | " +
                $"Controller: {animator.runtimeAnimatorController}",
                animator
            );
        }

        rb.freezeRotation = true;

        if (!view.IsMine)
        {
            cam.gameObject.SetActive(false);
            return;
        }

        cam.gameObject.SetActive(true);
        
        yaw = transform.eulerAngles.y; 
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (view.IsMine)
        {
            Look();
            Jump();
            UpdateAnimation();
        }
    }

    private void FixedUpdate()
    {
        if (view.IsMine)
        {
            Move();
            ApplyFallGravity();
        }
    }

    void LateUpdate()
    {
        if (view.IsMine)
        {
            
            PositionCamera();
        }
    }

    private void UpdateAnimation()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        Vector3 input = GetMoveInput();

        animator.SetFloat("Speed", input.magnitude, 0.1f, Time.deltaTime);
    }

    void Look()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        yaw += mouseDelta.x * mouseSensitivity;
        pitch -= mouseDelta.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -40f, 70f);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    Vector3 GetMoveInput()
    {
        var kb = Keyboard.current;

        float x = 0f, z = 0f;
        if (kb.wKey.isPressed) z += 1f;
        if (kb.sKey.isPressed) z -= 1f;
        if (kb.dKey.isPressed) x += 1f;
        if (kb.aKey.isPressed) x -= 1f;
        return new Vector3(x,0f,z).normalized;
    }

    void Jump()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame && IsGrounded())
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
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

    bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, 1.1f);
    }


    void PositionCamera()
    {
        Quaternion camRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 focusPoint = transform.position + Vector3.up * cameraHeight;

        cam.position = focusPoint - camRotation * Vector3.forward * cameraDistance + camRotation * Vector3.right * cameraSideOffset;
        cam.rotation = camRotation;
    }

    void Move()
    {
        Vector3 input = GetMoveInput();
        Vector3 moveDir = transform.forward * input.z + transform.right * input.x; 
        
        Vector3 velocity = moveDir * moveSpeed;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;
    }
}
