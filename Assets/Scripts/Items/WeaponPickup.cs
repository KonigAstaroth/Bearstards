
using UnityEngine;
using Photon.Pun;

public class WeaponPickup : MonoBehaviourPun
{
    public enum PickupType
    {
        Needle,
        Pistol
    }

    [Header("Configuración")]
    [SerializeField] private PickupType pickupType;
    [SerializeField] private bool canBeDestroyed;

    private bool used;

    private void OnTriggerEnter(Collider other)
    {
        if ((canBeDestroyed && used) || !PhotonNetwork.InRoom)
            return;

        WeaponController weapon =
            other.GetComponentInParent<WeaponController>();

        if (weapon == null || weapon.photonView == null)
            return;

        if (!weapon.photonView.IsMine)
            return;

        // No volver a recoger una pistola ya equipada.
        if (pickupType == PickupType.Pistol &&
            !weapon.CanPickUpPistol())
            return;

        // La aguja no reemplaza una pistola.
        if (pickupType == PickupType.Needle &&
            !weapon.CanPickUpNeedle())
            return;

        if (photonView == null || photonView.ViewID == 0)
        {
            Debug.LogError(
                $"El pickup {name} no tiene un PhotonView con ID válido.",
                this
            );
            return;
        }

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
        if (!PhotonNetwork.IsMasterClient ||
            (canBeDestroyed && used))
            return;

        PhotonView playerView = PhotonView.Find(playerViewID);

        if (playerView == null ||
            playerView.OwnerActorNr != info.Sender.ActorNumber)
            return;

        WeaponController weapon =
            playerView.GetComponent<WeaponController>();

        if (weapon == null)
            return;

        if (pickupType == PickupType.Pistol &&
            !weapon.CanPickUpPistol())
            return;

        if (pickupType == PickupType.Needle &&
            !weapon.CanPickUpNeedle())
            return;

        if (canBeDestroyed)
            used = true;

        photonView.RPC(
            nameof(GrantPickup),
            RpcTarget.All,
            playerViewID,
            (int)pickupType
        );
    }

    [PunRPC]
    private void GrantPickup(int playerViewID, int type)
    {
        if (canBeDestroyed && used == false)
            used = true;

        PhotonView playerView = PhotonView.Find(playerViewID);

        if (playerView != null)
        {
            if ((PickupType)type == PickupType.Needle)
            {
                playerView.RPC(
                    nameof(WeaponController.EquipNeedle),
                    RpcTarget.All
                );
            }
            else
            {
                playerView.RPC(
                    nameof(WeaponController.ShowWeapon),
                    RpcTarget.All
                );
            }
        }

        if (canBeDestroyed)
            gameObject.SetActive(false);
    }
}
