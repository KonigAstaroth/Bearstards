
using UnityEngine;
using Photon.Pun;

public class WeaponPickup : MonoBehaviourPun
{
    private bool used = false;

    private void OnTriggerEnter(Collider other)
    {
        if (used)
            return;

        WeaponController weapon =
            other.GetComponentInParent<WeaponController>();

        if (weapon == null || !weapon.photonView.IsMine)
            return;

        photonView.RPC(
            nameof(RequestPickup),
            RpcTarget.MasterClient,
            weapon.photonView.ViewID
        );
    }

    [PunRPC]
    private void RequestPickup(
        int playerViewID,
        PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || used)
            return;

        PhotonView playerView = PhotonView.Find(playerViewID);

        if (playerView == null ||
            playerView.OwnerActorNr != info.Sender.ActorNumber)
            return;

        WeaponController weapon =
            playerView.GetComponent<WeaponController>();

        if (weapon == null)
            return;

        used = true;

        photonView.RPC(
            nameof(GrantPickup),
            RpcTarget.All,
            playerViewID
        );
    }

    [PunRPC]
    private void GrantPickup(int playerViewID)
    {
        used = true;

        PhotonView playerView = PhotonView.Find(playerViewID);

        if (playerView != null)
        {
            playerView.RPC(
                "ShowWeapon",
                RpcTarget.All
            );
        }

        gameObject.SetActive(false);
    }
}
