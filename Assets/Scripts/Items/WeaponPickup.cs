using UnityEngine;
using Photon.Pun;

public class WeaponPickup : MonoBehaviourPun
{
    private void OnTriggerEnter (Collider other)
    {

        WeaponController weapon = other.GetComponent<WeaponController>();

        if(weapon != null && photonView.IsMine)
        {
            PhotonNetwork.Destroy(gameObject);
            weapon.PickUpWeapon();
        }

    }
}
