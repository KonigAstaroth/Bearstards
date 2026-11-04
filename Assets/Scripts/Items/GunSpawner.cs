using Photon.Pun;
using UnityEngine;

public class GunSpawner : MonoBehaviour
{
    [Header ("Toy Gun Prefab")]
    [SerializeField] private GameObject ToyGunPrefab;

    private Vector3[] spawnPositions =
    {
        new Vector3(-14.293f, 0.51f, 5f),
        new Vector3(-14.293f, 0.51f, 23f),
        new Vector3(-5.81f, 0.51f, 23f),
        new Vector3(-5.81f, 0.51f, 12f),
        new Vector3(-5.81f, 0.51f, -1f),
        new Vector3(4.24f, 0.51f, -1f),
        new Vector3(4.24f, 0.51f, 28.54f)
    };

    public void SpawnGun()
    {
        if (!PhotonNetwork.IsMasterClient){
            return;
        }

        int randomCoords = Random.Range(0, spawnPositions.Length);

        PhotonNetwork.Instantiate(ToyGunPrefab.name, spawnPositions[randomCoords], Quaternion.identity);
    }
}
