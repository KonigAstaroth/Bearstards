using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;

public class PlayerStateMachine : MonoBehaviour
{
    public enum PlayerState
    {
        Grounded,
        Jumping,
        Landing
    }

    [Header("References")]
    [SerializeField] private ThirdPersonController controller;
    [SerializeField] private Animator animator;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float jumpTakeoffTime = 0.3f;
    [SerializeField] private float jumpAnimationDuration = 0.6f;

    [Header("Animation")]
    [SerializeField] private string jumpStateName = "jumping";
    [SerializeField] private string idleStateName = "idle";
    [SerializeField] private string walkingStateName = "walking";
    [SerializeField] private float recoveryTimeout = 3f;

    public PlayerState CurrentState { get; private set; }
        = PlayerState.Grounded;

    private PhotonView view;
    private bool jumpRoutineRunning;

    private void Awake()
    {
        view = GetComponent<PhotonView>();

        if (controller == null)
            controller = GetComponent<ThirdPersonController>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (controller == null)
            Debug.LogError("Falta ThirdPersonController.", this);

        if (animator == null)
            Debug.LogError("Falta el Animator del jugador.", this);
    }

    private void Update()
    {
        if (view == null || !view.IsMine)
            return;

        if (controller == null || animator == null)
            return;

        // Actualizar locomoción.
        animator.SetFloat(
            "Speed",
            controller.GetMovementInputMagnitude(),
            0.1f,
            Time.deltaTime
        );

        // Solo se permite iniciar un salto desde Grounded.
        if (CurrentState == PlayerState.Grounded
            && !jumpRoutineRunning
            && Keyboard.current != null
            && Keyboard.current.spaceKey.wasPressedThisFrame
            && controller.IsGrounded())
        {
            StartCoroutine(JumpRoutine());
        }
    }

    private IEnumerator JumpRoutine()
    {
        jumpRoutineRunning = true;
        CurrentState = PlayerState.Jumping;

        // Iniciar animación de salto
        animator.SetBool("IsJumping", true);
        animator.SetBool("IsGrounded", false);
        animator.CrossFade(jumpStateName, 0.05f, 0, 0f);

        // Esperar hasta el momento del impulso
        yield return new WaitForSeconds(jumpTakeoffTime);

        // Aplicar impulso físico
        controller.ApplyJumpImpulse(jumpForce);

        // Completar el tiempo restante de la animación
        float remainingAnimationTime =
            Mathf.Max(0f, jumpAnimationDuration - jumpTakeoffTime);

        yield return new WaitForSeconds(remainingAnimationTime);

        // Esperar a que realmente despegue
        float elapsed = 0f;
        const float takeoffTimeout = 1f;

        while (controller.IsGrounded() && elapsed < takeoffTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Si no despegó, recuperar el estado
        if (controller.IsGrounded())
        {
            Debug.LogWarning(
                "El jugador no despegó. Revisa el ground check y jumpForce."
            );

            animator.SetBool("IsJumping", false);
            animator.SetBool("IsGrounded", true);

            CurrentState = PlayerState.Grounded;
            jumpRoutineRunning = false;
            yield break;
        }

        // Esperar hasta tocar el suelo
        yield return new WaitUntil(() => controller.IsGrounded());

        // Entrar en recuperación/aterrizaje
        CurrentState = PlayerState.Landing;

        animator.SetBool("IsGrounded", true);
        animator.SetBool("IsJumping", false);

        // Esperar a que el Animator regrese a idle o walking
        elapsed = 0f;

        while (elapsed < recoveryTimeout)
        {
            AnimatorStateInfo state =
                animator.GetCurrentAnimatorStateInfo(0);

            bool returnedToLocomotion =
                !animator.IsInTransition(0) &&
                (state.IsName(idleStateName) ||
                state.IsName(walkingStateName));

            if (returnedToLocomotion)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (elapsed >= recoveryTimeout)
        {
            Debug.LogWarning(
                "Terminó el tiempo de recuperación. " +
                "Revisa los nombres y las transiciones del Animator."
            );
        }

        // Permitir volver a saltar
        CurrentState = PlayerState.Grounded;
        jumpRoutineRunning = false;
    }
}
