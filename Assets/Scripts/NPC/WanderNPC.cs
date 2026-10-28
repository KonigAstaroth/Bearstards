using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NPC que deambula libremente por todo el NavMesh.
/// - Elige destinos a distancia variable desde su posición actual (no desde donde nació).
/// - Se detiene a esperar, a veces mira a su alrededor y a veces "curiosea" más tiempo.
/// - Cada NPC tiene su propia velocidad, para que no caminen todos igual.
/// - Si se atasca, elige otro destino.
/// Requiere un NavMesh horneado en la escena. No necesita PatrolZone.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class WanderNPC : MonoBehaviour
{
    [Header("Recorrido")]
    [Tooltip("Distancia mínima del siguiente destino (evita dar vueltas en el mismo lugar).")]
    [SerializeField] private float minDistance = 8f;
    [Tooltip("Distancia máxima del siguiente destino.")]
    [SerializeField] private float maxDistance = 35f;

    [Header("Pausas")]
    [SerializeField] private Vector2 waitTimeRange = new Vector2(1.5f, 8f);
    [Range(0f, 1f)]
    [SerializeField] private float browseChance = 0.2f;                       // prob. de pausa larga
    [SerializeField] private Vector2 browseTimeRange = new Vector2(5f, 10f);  // duración de pausa larga
    [Range(0f, 1f)]
    [SerializeField] private float lookAroundChance = 0.6f;                   // prob. de girar al esperar
    [SerializeField] private float turnSpeed = 120f;                          // grados por segundo

    [Header("Movimiento")]
    [SerializeField] private Vector2 speedRange = new Vector2(1.8f, 3.5f);

    [Header("Animación")]
    [Tooltip("Si se deja vacío, lo busca en este objeto o en sus hijos.")]
    [SerializeField] private Animator animator;

    [Header("Anti-atasco")]
    [SerializeField] private float stuckTime = 3f;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsWaitingHash = Animator.StringToHash("IsWaiting");

    private NavMeshAgent agent;
    private NavMeshPath path;

    private bool waiting;
    private bool lookingAround;
    private float waitTimer;
    private float currentWait;
    private float stuckTimer;
    private Quaternion lookTarget;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        path = new NavMeshPath();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        // Cada NPC con su propio ritmo
        agent.speed = Random.Range(speedRange.x, speedRange.y);
        agent.acceleration = 8f;
        agent.angularSpeed = 180f;
        agent.stoppingDistance = Mathf.Max(agent.stoppingDistance, 0.3f);
        agent.avoidancePriority = Random.Range(30, 70);

        // Arranque escalonado: no todos echan a andar al mismo tiempo
        waiting = true;
        lookingAround = false;
        waitTimer = 0f;
        currentWait = Random.Range(0f, 2f);
        SetAnimation(false, true);
    }

    private void Update()
    {
        if (!agent.isOnNavMesh) return;

        if (waiting)
        {
            HandleWait();
            return;
        }

        if (agent.pathPending) return;

        // Llegó al destino
        if (agent.remainingDistance <= agent.stoppingDistance)
        {
            BeginWait();
            return;
        }

        // Detección de atasco
        if (agent.velocity.sqrMagnitude < 0.01f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= stuckTime)
            {
                stuckTimer = 0f;
                PickNewDestination();
            }
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    private void BeginWait()
    {
        waiting = true;
        waitTimer = 0f;
        stuckTimer = 0f;
        SetAnimation(false, true);

        bool browse = Random.value < browseChance;
        currentWait = browse
            ? Random.Range(browseTimeRange.x, browseTimeRange.y)
            : Random.Range(waitTimeRange.x, waitTimeRange.y);

        lookingAround = Random.value < lookAroundChance;
        if (lookingAround)
            lookTarget = transform.rotation * Quaternion.Euler(0f, Random.Range(-120f, 120f), 0f);
    }

    private void HandleWait()
    {
        waitTimer += Time.deltaTime;

        if (lookingAround)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, lookTarget, turnSpeed * Time.deltaTime);

        if (waitTimer >= currentWait)
        {
            waiting = false;
            PickNewDestination();
        }
    }

    private void PickNewDestination()
    {
        for (int i = 0; i < 15; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            float distance = Random.Range(minDistance, maxDistance);
            Vector3 candidate = transform.position + new Vector3(dir.x, 0f, dir.y) * distance;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas)
                && agent.CalculatePath(hit.position, path)
                && path.status == NavMeshPathStatus.PathComplete)
            {
                agent.SetPath(path);
                SetAnimation(true, false);
                return;
            }
        }

        // No encontró un destino válido: espera un poco y reintenta
        BeginWait();
    }

    private void SetAnimation(bool isMoving, bool isWaiting)
    {
        if (animator == null) return;

        animator.SetBool(IsMovingHash, isMoving);
        animator.SetBool(IsWaitingHash, isWaiting);
    }
}