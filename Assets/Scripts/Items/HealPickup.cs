using UnityEngine;
using Photon.Pun;

public class HealOnCollision : MonoBehaviourPun
{
    [SerializeField] private int healPoints = 25;
    private void OnTriggerEnter(Collider other)
    {
        HealthSystem health = other.GetComponent<HealthSystem>();
        if (health != null && health.photonView.IsMine)
        {
            health.Heal(healPoints);
            PhotonNetwork.Destroy(gameObject);
        }
    }
}

