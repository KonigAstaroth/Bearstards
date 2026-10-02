using UnityEngine;
using Photon.Pun;

public class SpawnPlayers : MonoBehaviour
{
    public GameObject playerPrefab;

    public float areaWidth = 5f;
    public float areaLength = 5f;

    public float playerRadius = 0.4f;
    public LayerMask obstacleLayer;

    public int maxAttempts = 20;

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

    private void SpawnPlayer()
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
                0f,
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

                return;
            }
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