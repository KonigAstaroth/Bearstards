
using UnityEngine;
using Photon.Pun;

public class DartProjectile : MonoBehaviourPun, IPunInstantiateMagicCallback
{
    [SerializeField] private int damage = 25;

    private int attackerViewID = -1;
    private int pointsPerKill = 0;

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        object[] data = photonView.InstantiationData;

        if (data == null || data.Length < 2)
        {
            return;
        }

        attackerViewID = (int)data[0];
        pointsPerKill = (int)data[1];

    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!photonView.IsMine)
            return;

        HealthSystem health =
            collision.gameObject.GetComponentInParent<HealthSystem>();

        if (health != null)
        {

            health.photonView.RPC(
                nameof(HealthSystem.TakeDamage),
                health.photonView.Owner,
                damage,
                attackerViewID,
                pointsPerKill
            );
        }

        PhotonNetwork.Destroy(gameObject);
    }
}
