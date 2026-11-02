using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Genera NPCs sobre el NavMesh al iniciar la escena.
/// - Con sala de Photon: solo el MasterClient los crea, con PhotonNetwork.Instantiate
///   (el prefab debe estar dentro de una carpeta Resources).
/// - Sin sala (pruebas sueltas): los crea localmente con el prefab de "Offline Prefab".
/// Requiere un NavMesh horneado en la escena.
/// </summary>
public class NPCSpawner : MonoBehaviour
{
    [Header("NPC")]
    [Tooltip("Nombre exacto del prefab (sin .prefab) que está dentro de una carpeta Resources.")]
    [SerializeField] private string npcPrefabName = "NPC_Patrulla";
    [Tooltip("Solo para pruebas sin sala de Photon. Se puede dejar vacío.")]
    [SerializeField] private GameObject offlinePrefab;

    [Header("Cantidad y zona")]
    [SerializeField] private int count = 8;
    [Tooltip("Radio alrededor de este objeto donde se buscan puntos de aparición.")]
    [SerializeField] private float spawnRadius = 35f;
    [Tooltip("Separación mínima entre NPCs al aparecer.")]
    [SerializeField] private float minSeparation = 4f;

    private void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            // Solo el MasterClient genera los NPCs, para que no se dupliquen
            if (PhotonNetwork.IsMasterClient)
                SpawnAll();
        }
        else if (offlinePrefab != null)
        {
            SpawnAll();
        }
    }

    private void SpawnAll()
    {
        List<Vector3> used = new List<Vector3>();
        int spawned = 0;

        for (int i = 0; i < count; i++)
        {
            if (!TryGetSpawnPoint(used, out Vector3 point))
            {
                Debug.LogWarning($"NPCSpawner: solo se encontró espacio para {spawned} de {count} NPCs.");
                break;
            }

            used.Add(point);
            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            if (PhotonNetwork.InRoom)
                PhotonNetwork.Instantiate(npcPrefabName, point, rotation);
            else
                Instantiate(offlinePrefab, point, rotation);

            spawned++;
        }
    }

    private bool TryGetSpawnPoint(List<Vector3> used, out Vector3 result)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            Vector3 candidate = transform.position + new Vector3(offset.x, 0f, offset.y);

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                continue;

            bool tooClose = false;
            foreach (Vector3 other in used)
            {
                if (Vector3.Distance(other, hit.position) < minSeparation)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose) continue;

            result = hit.position;
            return true;
        }

        result = Vector3.zero;
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}
