using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NPC que patrulla entre waypoints usando NavMeshAgent.
/// Requiere un NavMesh horneado en la escena.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class PatrolNPC : MonoBehaviour
{
    [Header("Patrulla")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float waitTime = 1.5f;   // segundos de espera en cada punto

    private NavMeshAgent agent;
    private int currentIndex;
    private float waitTimer;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning($"{name}: no hay waypoints asignados.");
            enabled = false;
            return;
        }

        agent.SetDestination(waypoints[currentIndex].position);
    }

    private void Update()
    {
        // Espera a que Unity termine de calcular la ruta
        if (agent.pathPending) return;

        // ¿Llegó al waypoint actual?
        if (agent.remainingDistance <= agent.stoppingDistance)
        {
            waitTimer += Time.deltaTime;

            if (waitTimer >= waitTime)
            {
                waitTimer = 0f;
                currentIndex = (currentIndex + 1) % waypoints.Length; // vuelve al primero al terminar
                agent.SetDestination(waypoints[currentIndex].position);
            }
        }
    }

    // Dibuja la ruta en el editor para ver los waypoints
    private void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Length < 2) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            Gizmos.DrawSphere(waypoints[i].position, 0.25f);

            Transform next = waypoints[(i + 1) % waypoints.Length];
            if (next != null)
                Gizmos.DrawLine(waypoints[i].position, next.position);
        }
    }
}
