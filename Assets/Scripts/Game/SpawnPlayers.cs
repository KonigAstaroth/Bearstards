
using UnityEngine;
using Photon.Pun;
using System.Collections;

public class SpawnPlayers : MonoBehaviour
{
    [Header("Player")]
    public GameObject playerPrefab;

    [Header("Spawn")]
    public float areaWidth = 5f;
    public float areaLength = 5f;
    public float playerRadius = 0.4f;
    public LayerMask obstacleLayer;
    public int maxAttempts = 20;

    [Header("Respawn")]
    public float waitTimeRespawn = 7f;
    public RespawningUI respawnText;
    public Camera deathCamera;

    private bool isRespawning;

    private void Start()
    {
        StartCoroutine(WaitForRoom());
    }

    private IEnumerator WaitForRoom()
    {
        while (!PhotonNetwork.InRoom)
            yield return null;

        SpawnPlayer();
    }

    public void RespawnPlayer(GameObject deadPlayer)
    {
        if (isRespawning || deadPlayer == null)
            return;

        PhotonView view = deadPlayer.GetComponent<PhotonView>();

        if (view == null || !view.IsMine)
            return;

        isRespawning = true;
        StartCoroutine(RespawnCoroutine(deadPlayer));
    }

    private IEnumerator RespawnCoroutine(GameObject deadPlayer)
    {
        if (deathCamera != null)
            deathCamera.enabled = true;

        PhotonNetwork.Destroy(deadPlayer);

        float remaining = waitTimeRespawn;

        while (remaining > 0f)
        {
            if (respawnText != null)
                respawnText.SetCountDown(Mathf.CeilToInt(remaining));

            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        if (respawnText != null)
            respawnText.SetCountDown(0);

        SpawnPlayer();

        if (deathCamera != null)
            deathCamera.enabled = false;

        isRespawning = false;
    }

    private void SpawnPlayer()
    {
        if (!PhotonNetwork.InRoom)
            return;

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector3 randomPosition = transform.position + new Vector3(
                Random.Range(-areaWidth / 2f, areaWidth / 2f),
                0f,
                Random.Range(-areaLength / 2f, areaLength / 2f)
            );

            bool occupied = Physics.CheckSphere(
                randomPosition,
                playerRadius,
                obstacleLayer
            );

            if (occupied)
                continue;

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

            return;
        }

        Debug.LogWarning("No se encontró un punto de spawn libre.");
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(
            transform.position,
            new Vector3(areaWidth, 0.1f, areaLength)
        );
    }
}
