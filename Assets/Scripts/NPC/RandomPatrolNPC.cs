using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NPC que elige destinos al azar.
/// - Si hay un PatrolZone en la escena, va a waypoints aleatorios (nunca repite el anterior).
/// - Si no hay PatrolZone, camina a puntos aleatorios del NavMesh cerca de donde nació.
/// Requiere un NavMesh horneado en la escena.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class RandomPatrolNPC : MonoBehaviour
{
    [Header("Zona (opcional)")]
    [Tooltip("Si se deja vacío, busca un PatrolZone en la escena al iniciar.")]
    [SerializeField] private PatrolZone zone;

    [Header("Modo libre (sin PatrolZone)")]
    [SerializeField] private float wanderRadius = 15f;

    [Header("Variación")]
    [SerializeField] private Vector2 waitTimeRange = new Vector2(1f, 4f);   // segundos de espera
    [SerializeField] private Vector2 speedRange = new Vector2(2.5f, 4.5f);  // velocidad por NPC

    private NavMeshAgent agent;
    private Transform lastPoint;
    private Vector3 origin;
    private bool waiting;
    private float waitTimer;
    private float currentWait;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        origin = transform.position;

        if (zone == null)
            zone = FindFirstObjectByType<PatrolZone>();

        // Cada NPC se comporta un poco distinto
        agent.speed = Random.Range(speedRange.x, speedRange.y);
        agent.avoidancePriority = Random.Range(30, 70); // evita que se bloqueen entre ellos

        PickNewDestination();
    }

    private void Update()
    {
        if (!agent.isOnNavMesh || agent.pathPending) return;

        if (agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!waiting)
            {
                waiting = true;
                waitTimer = 0f;
                currentWait = Random.Range(waitTimeRange.x, waitTimeRange.y);
            }

            waitTimer += Time.deltaTime;

            if (waitTimer >= currentWait)
            {
                waiting = false;
                PickNewDestination();
            }
        }
    }

    private void PickNewDestination()
    {
        if (!agent.isOnNavMesh) return;

        Vector3 target;

        if (zone != null && zone.HasPoints)
        {
            lastPoint = zone.GetRandomPoint(lastPoint);
            target = lastPoint.position;
        }
        else if (!TryGetRandomNavMeshPoint(origin, wanderRadius, out target))
        {
            return; // no encontró punto válido; reintenta en el siguiente ciclo
        }

        agent.SetDestination(target);
    }

    private bool TryGetRandomNavMeshPoint(Vector3 center, float radius, out Vector3 result)
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 randomPoint = center + Random.insideUnitSphere * radius;

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }

        result = center;
        return false;
    }
}