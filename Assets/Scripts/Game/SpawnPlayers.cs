using UnityEngine;
using Photon.Pun;

public class SpawnPlayers : MonoBehaviour
{
    public GameObject playerPrefab;

    public Camera deathCamera;

    public float areaWidth = 5f;
    public float areaLength = 5f;

    public float playerRadius = 0.4f;
    public LayerMask obstacleLayer;

    public int maxAttempts = 20;

    [SerializeField] private RespawningUI respawnText;

    private void Start()
    {
        StartCoroutine(WaitForRoom());
    }

    private System.Collections.IEnumerator WaitForRoom()
    {
        while (!PhotonNetwork.InRoom)
        {
            yield return null;
        }

        SpawnPlayer();
    }

    public GameObject SpawnPlayer()
    {
        Debug.Log(
            $"SPAWNING PLAYER | " +
            $"InRoom: {PhotonNetwork.InRoom} | " +
            $"Actor: {PhotonNetwork.LocalPlayer.ActorNumber}"
        );

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector3 randomPosition = transform.position + new Vector3(
                Random.Range(-areaWidth / 2f, areaWidth / 2f),
                1f,
                Random.Range(-areaLength / 2f, areaLength / 2f)
            );

            bool occupied = Physics.CheckSphere(
                randomPosition,
                playerRadius,
                obstacleLayer
            );

            if (!occupied)
            {
                GameObject player = PhotonNetwork.Instantiate(
                    playerPrefab.name,
                    randomPosition,
                    Quaternion.identity
                );

                Debug.Log(
                    $"PLAYER CREATED | " +
                    $"ViewID: {player.GetComponent<PhotonView>().ViewID} | " +
                    $"IsMine: {player.GetComponent<PhotonView>().IsMine}"
                );

                return player;
            }
        }
        Debug.LogWarning("No se encontró una posición válida para respawnear.");
        return null;

    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(
            transform.position,
            new Vector3(areaWidth, 0.1f, areaLength)
        );
    }

    public void RespawnPlayer(GameObject oldPlayer)
    {
        StartCoroutine(RespawnCoroutine(oldPlayer));
    }

    private System.Collections.IEnumerator RespawnCoroutine(GameObject oldPlayer)
    {
        Debug.Log($"respawnText null? {respawnText == null} | objeto: {name}", this);
        if (deathCamera != null)
        {
            deathCamera.gameObject.SetActive(true);
        }

        PhotonNetwork.Destroy(oldPlayer);

        int waitTimeRespawn = 5;

        while (waitTimeRespawn > 0)
        {
            respawnText.SetCountDown(waitTimeRespawn);
            yield return new WaitForSeconds(1f);
            waitTimeRespawn--;
        }

        respawnText.SetCountDown(0);
        yield return new WaitForSeconds(1.5f);

        if (deathCamera != null)
        {
            deathCamera.gameObject.SetActive(false);
        }

        SpawnPlayer();
    }
}