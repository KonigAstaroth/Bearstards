using UnityEngine;
using Photon.Pun;
using System.Collections;

public class HealOnCollision : MonoBehaviourPun
{
    [Header("Healing points")]
    [SerializeField] private int healPoints = 25;

    [Header("Pickup")]
    [SerializeField] private Collider pickupCollider;
    [SerializeField] private GameObject healModel;

    [Header("Respawn")]
    [SerializeField] private float RespawnTime = 20f;

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

        used = true;
        health.Heal(healPoints);

        photonView.RPC(nameof(DisableHeal), RpcTarget.All);
    }

    [PunRPC]
    private void DisableHeal()
    {
        used = true;
        if (healModel != null)
        {
            healModel.SetActive(false);
        }

        if (pickupCollider != null)
        {
            pickupCollider.enabled = false;
        }

        StopAllCoroutines();
        StartCoroutine(RespawnCoroutine());
    }

    private IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(RespawnTime);

        if (healModel != null)
        {
            healModel.SetActive(true);
        }

        if (pickupCollider != null)
        {
            pickupCollider.enabled = true;
        }

        used = false;
    }
}

