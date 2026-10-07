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

    private bool hasWeapon = false;

    private void Start()
    {
        WeaponModel.SetActive(false);
    }

    public void PickUpWeapon()
    {
        if (!photonView.IsMine)
        {
            return;
        }

        hasWeapon = true;
        WeaponModel.SetActive(true);
    }

    // Update is called once per frame
    private void Update()
    {
        if (!hasWeapon)
        {
            return;
        }

        if (!photonView.IsMine)
        {
            return;
        }

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
            shootPoint.rotation
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
