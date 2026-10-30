using Photon.Pun;
using System.Collections;
using UnityEngine;

public class ShieldPickup : MonoBehaviourPun
{
    [Header("Pickup")]
    [SerializeField] private Collider pickupCollider;
    [SerializeField] private GameObject shieldModel;

    [Header("Respawn")]
    [SerializeField] private float respawnTime = 20f;

    private bool used = false;
    private void OnTriggerEnter(Collider other)
    {
        if (used)
        {
            return;
        }

        HealthSystem health = other.GetComponentInParent<HealthSystem>();
        if (health == null || !health.photonView.IsMine)
        {
            return;
        }

        if (health.GetCurrentShield() > 0)
        {
            return;
        }

        used = true;
        health.ActivateShield();

        photonView.RPC(nameof(DisableShield), RpcTarget.All);
    }

    [PunRPC]
    private void DisableShield()
    {
        used = true;

        if (pickupCollider != null)
        {
            pickupCollider.enabled = false;
        }

        if (shieldModel != null)
        {
            shieldModel.SetActive(false);
        }

        StopAllCoroutines();
        StartCoroutine(RespawnCoroutine());
    }
    private IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(respawnTime);

        Debug.Log("Aqui deberia aparecer el casco");

        if (pickupCollider != null)
        {
            pickupCollider.enabled = true;
        }

        if (shieldModel != null)
        {
            shieldModel.SetActive(true);
        }

        used = false;
    }
}
