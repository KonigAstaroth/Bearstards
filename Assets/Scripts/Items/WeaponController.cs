
using UnityEngine;
using Photon.Pun;

public class WeaponController : MonoBehaviourPun
{
    [Header("Weapon")]
    [SerializeField] private GameObject WeaponModel;
    [SerializeField] private Transform WeaponPoint;
    [SerializeField] private GameObject DartPrefab;

    [Header("Shooting")]
    [SerializeField] private Transform shootPoint;
    [SerializeField] private float shootForce = 100f;
    [SerializeField] private int pointsPerKill = 400;

    private bool hasWeapon = false;

    public bool HasWeapon => hasWeapon;

    private void Start()
    {
        WeaponModel.SetActive(false);
    }

    public bool PickUpWeapon()
    {
        if (hasWeapon || !photonView.IsMine)
            return false;

        photonView.RPC(nameof(ShowWeapon), RpcTarget.All);
        return true;
    }

    [PunRPC]
    public void ShowWeapon()
    {
        if(hasWeapon) return;

        hasWeapon = true;
        WeaponModel.SetActive(true);
    }

    private void Update()
    {
        if (!hasWeapon || !photonView.IsMine)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            Shoot();
        }
    }

    private void Shoot()
    {
        GameObject dart = PhotonNetwork.Instantiate(
            DartPrefab.name,
            shootPoint.position,
            shootPoint.rotation,
            0,
            new object[]
            {
                photonView.ViewID,
                pointsPerKill
            }
        );

        Rigidbody rb = dart.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.AddForce(
                shootPoint.forward * shootForce,
                ForceMode.Impulse
            );
        }
    }
}
