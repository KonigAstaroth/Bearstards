using UnityEngine;

/// <summary>
/// Agrupa los waypoints de una zona. Todos los hijos de este objeto
/// se toman como puntos de patrulla (agrega o quita hijos libremente).
/// </summary>
public class PatrolZone : MonoBehaviour
{
    private Transform[] points;

    public bool HasPoints => points != null && points.Length > 0;

    private void Awake()
    {
        points = new Transform[transform.childCount];
        for (int i = 0; i < points.Length; i++)
            points[i] = transform.GetChild(i);
    }

    /// <summary>
    /// Devuelve un waypoint al azar, evitando repetir el anterior (si hay más de uno).
    /// </summary>
    public Transform GetRandomPoint(Transform exclude = null)
    {
        if (!HasPoints) return null;
        if (points.Length == 1) return points[0];

        Transform chosen;
        do
        {
            chosen = points[Random.Range(0, points.Length)];
        }
        while (chosen == exclude);

        return chosen;
    }

    // Dibuja los puntos en el editor
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        for (int i = 0; i < transform.childCount; i++)
            Gizmos.DrawSphere(transform.GetChild(i).position, 0.3f);
    }
}
