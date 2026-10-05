using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;

[RequireComponent(typeof(Rigidbody))]
public class ThirdPersonController : MonoBehaviour
{
    public Transform cam;
    public float moveSpeed = 6f;
    public float jumpForce = 7f;

    public float mouseSensitivity = 0.12f;
    public float cameraDistance = 5f;
    public float cameraHeight = 1.6f;

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
        }
    }

    private void FixedUpdate()
    {
        if (view.IsMine)
        {
            Move();
        }
    }

    void LateUpdate()
    {
        if (view.IsMine)
        {
            
            PositionCamera();
        }
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

    bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, 1.1f);
    }


    void PositionCamera()
    {
        Quaternion camRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 focusPoint = transform.position + Vector3.up * cameraHeight;

        cam.position = focusPoint - camRotation * Vector3.forward * cameraDistance;
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
