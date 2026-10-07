using UnityEngine;
using Photon.Pun;

public class HealOnCollision : MonoBehaviourPun
{
    [SerializeField] private int healPoints = 25;

    private bool used = false;
    private void OnTriggerEnter(Collider other)
    {
        if (used)
        {
            return;
        }

        HealthSystem health = other.GetComponent<HealthSystem>();
        if (health == null || !health.photonView.IsMine)
        {
            return;
        }

        if (health.HasFullHealth())
        {
            return;
        }

        health.Heal(healPoints);

        photonView.RPC(nameof(DisableHeal), RpcTarget.All);
    }

    [PunRPC]
    private void DisableHeal()
    {
        gameObject.SetActive(false);
    }
}

