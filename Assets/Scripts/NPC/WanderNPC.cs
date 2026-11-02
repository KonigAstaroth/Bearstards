using System.Collections;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NPC que deambula libremente por el NavMesh, con animaciones, detección de obstáculos
/// y sistema de vida (HealthSystem).
/// - Solo el MasterClient simula la IA; los demás clientes reciben posición y animación por Photon.
/// - Si no hay sala de Photon (pruebas offline), simula localmente.
/// Requiere un NavMesh horneado en la escena.
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
    [SerializeField] private float browseChance = 0.2f;
    [SerializeField] private Vector2 browseTimeRange = new Vector2(5f, 10f);
    [Range(0f, 1f)]
    [SerializeField] private float lookAroundChance = 0.6f;
    [SerializeField] private float turnSpeed = 120f;

    [Header("Movimiento")]
    [SerializeField] private Vector2 speedRange = new Vector2(1.8f, 3.5f);

    [Header("Detección de obstáculos")]
    [Tooltip("Capas que debe esquivar (jugadores, otros NPCs, objetos). Paredes y estantes ya los evita el NavMesh.")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private float detectDistance = 2.5f;
    [SerializeField] private float detectRadius = 0.4f;
    [SerializeField] private float detectHeight = 1f;
    [Tooltip("Segundos bloqueado de frente antes de elegir otra ruta.")]
    [SerializeField] private float blockedRepathTime = 1.5f;

    [Header("Animación")]
    [Tooltip("Si se deja vacío, lo busca en este objeto o en sus hijos.")]
    [SerializeField] private Animator animator;
    [Tooltip("Nombre de un parámetro Trigger de muerte (opcional). Vacío = sin animación de muerte.")]
    [SerializeField] private string deathTrigger = "";

    [Header("Muerte")]
    [SerializeField] private float destroyDelay = 2f;

    [Header("Anti-atasco")]
    [SerializeField] private float stuckTime = 3f;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsWaitingHash = Animator.StringToHash("IsWaiting");

    private NavMeshAgent agent;
    private NavMeshPath path;
    private HealthSystem health;

    private bool waiting;
    private bool lookingAround;
    private bool isDead;
    private float waitTimer;
    private float currentWait;
    private float stuckTimer;
    private float blockedTimer;
    private float baseSpeed;
    private Quaternion lookTarget;

    // Solo el MasterClient (o nadie, si no hay sala) simula la IA
    private bool ShouldSimulate => !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        path = new NavMeshPath();
        health = GetComponent<HealthSystem>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        if (health != null)
            health.Died += HandleDeath;
    }

    private void OnDisable()
    {
        if (health != null)
            health.Died -= HandleDeath;
    }

    private void Start()
    {
        // En los demás clientes, el movimiento llega por PhotonTransformView
        if (!ShouldSimulate)
        {
            agent.enabled = false;
            enabled = false;
            return;
        }

        baseSpeed = Random.Range(speedRange.x, speedRange.y);
        agent.speed = baseSpeed;
        agent.acceleration = 8f;
        agent.angularSpeed = 180f;
        agent.stoppingDistance = Mathf.Max(agent.stoppingDistance, 0.3f);
        agent.avoidancePriority = Random.Range(30, 70);
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

        // Arranque escalonado: no todos echan a andar al mismo tiempo
        waiting = true;
        lookingAround = false;
        waitTimer = 0f;
        currentWait = Random.Range(0f, 2f);
        SetAnimation(false, true);
    }

    private void Update()
    {
        if (isDead || !agent.isOnNavMesh) return;

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

        AvoidObstacles();

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

    // ---------- Obstáculos ----------

    private void AvoidObstacles()
    {
        Vector3 dir = agent.velocity.sqrMagnitude > 0.01f ? agent.velocity.normalized : transform.forward;
        Vector3 origin = transform.position + Vector3.up * detectHeight;

        bool blocked = Physics.SphereCast(origin, detectRadius, dir, out RaycastHit hit,
                                          detectDistance, obstacleMask, QueryTriggerInteraction.Ignore)
                       && !hit.collider.transform.IsChildOf(transform);

        if (blocked)
        {
            // Frena de forma proporcional a lo cerca que está el obstáculo
            float t = Mathf.Clamp01(hit.distance / detectDistance);
            agent.speed = Mathf.Lerp(baseSpeed * 0.3f, baseSpeed, t);

            // Si sigue bloqueado de frente, elige otra ruta
            if (hit.distance < 1f)
            {
                blockedTimer += Time.deltaTime;
                if (blockedTimer >= blockedRepathTime)
                {
                    blockedTimer = 0f;
                    PickNewDestination();
                }
            }
            else
            {
                blockedTimer = 0f;
            }
        }
        else
        {
            agent.speed = Mathf.MoveTowards(agent.speed, baseSpeed, agent.acceleration * Time.deltaTime);
            blockedTimer = 0f;
        }
    }

    // ---------- Pausas y destinos ----------

    private void BeginWait()
    {
        waiting = true;
        waitTimer = 0f;
        stuckTimer = 0f;
        blockedTimer = 0f;
        agent.speed = baseSpeed;
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

    // ---------- Vida ----------

    private void HandleDeath()
    {
        if (isDead) return;
        isDead = true;
        waiting = false;

        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        SetAnimation(false, false);

        if (animator != null && !string.IsNullOrEmpty(deathTrigger))
            animator.SetTrigger(deathTrigger);

        // Sin colisiones para que los proyectiles y jugadores lo atraviesen
        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;

        StartCoroutine(DestroyAfterDelay());
    }

    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(destroyDelay);

        if (PhotonNetwork.InRoom && GetComponent<PhotonView>() != null)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    // ---------- Animación ----------

    private void SetAnimation(bool isMoving, bool isWaiting)
    {
        if (animator == null) return;

        animator.SetBool(IsMovingHash, isMoving);
        animator.SetBool(IsWaitingHash, isWaiting);
    }
}