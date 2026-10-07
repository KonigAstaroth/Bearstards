using UnityEngine;
using Photon.Pun;

public class WeaponPickup : MonoBehaviourPun
{
    private bool used = false;

    private void OnTriggerEnter (Collider other)
    {
        if (used)
        {
            return;
        }
        WeaponController weapon = other.GetComponent<WeaponController>();

        if(weapon == null || !photonView.IsMine)
        {
            return;
        }

        if (!weapon.PickUpWeapon())
        {
            return;
        }

        used = true;
        photonView.RPC(nameof(DisablePickup), RpcTarget.All);

    }

    [PunRPC]
    private void DisablePickup()
    {
        gameObject.SetActive(false);
    }
}
