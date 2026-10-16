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
    private bool wasAirborne = false;
    private bool jumpInProgress;

    [SerializeField] private float jumpAnimationDuration = 0.6f;
    [SerializeField] private float jumpTakeoffTime = 0.3f;
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

            if (!jumpInProgress && wasAirborne)
            {
                if (!IsGrounded())
                {
                    animator.SetBool("IsGrounded", false);
                }
            }
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

        // Move
        Vector3 input = GetMoveInput();

        animator.SetFloat("Speed", input.magnitude, 0.1f, Time.deltaTime);

        // Landing
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
        if (Keyboard.current.spaceKey.wasPressedThisFrame && IsGrounded() && !jumpInProgress && !wasAirborne)
        {
            StartCoroutine(JumpRoutine());
            
        }
    }

   private System.Collections.IEnumerator JumpRoutine()
    {
        jumpInProgress = true;
        wasAirborne = false;

        if (animator != null)
        {
            animator.SetBool("IsJumping", true);
            animator.CrossFade("jumping", 0.05f, 0, 0f);

            // Esperar hasta que el oso tome impulso.
            yield return new WaitForSeconds(jumpTakeoffTime);
        }

        // Despegar a mitad de la animación.
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        wasAirborne = true;

        // Dejar que termine la animación en el aire.
        float remainingTime = Mathf.Max(
            0f,
            jumpAnimationDuration - jumpTakeoffTime
        );

        yield return new WaitForSeconds(remainingTime);
        StartCoroutine(WaitForLandingRecovery());

    }

    private System.Collections.IEnumerator WaitForLandingRecovery()
    {
        // Esperar hasta tocar el suelo.
        yield return new WaitUntil(() => IsGrounded());

        if (animator != null)
        {
            animator.SetBool("IsGrounded", true);
            animator.SetBool("IsJumping", false);

            // Esperar a que termine la animación de aterrizaje.
            float timeout = 3f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                var state = animator.GetCurrentAnimatorStateInfo(0);

                if (!animator.IsInTransition(0)
                    && (state.IsName("idle") || state.IsName("walking")))
                {
                    break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        wasAirborne = false;
        jumpInProgress = false;
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
        return Physics.Raycast(transform.position, Vector3.down, 1f);
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
