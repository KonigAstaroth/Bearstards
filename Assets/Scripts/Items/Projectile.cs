using UnityEngine;
using Photon.Pun;

public class DartProjectile : MonoBehaviourPun
{
    [SerializeField] private int damage = 25;

    private void OnCollisionEnter(Collision collision)
    {
        if (!photonView.IsMine)
            return;

        HealthSystem health = collision.gameObject.GetComponent<HealthSystem>();

        if (health != null)
        {
            health.TakeDamage(damage);
        }

        PhotonNetwork.Destroy(gameObject);
    }
}
