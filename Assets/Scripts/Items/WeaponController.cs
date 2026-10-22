
using UnityEngine;
using Photon.Pun;

public class WeaponController : MonoBehaviourPun
{
    public enum WeaponType
    {
        None,
        Needle,
        Pistol
    }

    [Header("Modelos")]
    [SerializeField] private GameObject NeedleModel;
    [SerializeField] private GameObject WeaponModel;

    [Header("Combate cuerpo a cuerpo")]
    [SerializeField] private NeedleMelee needleMelee;

    [Header("Pistola")]
    [SerializeField] private GameObject DartPrefab;
    [SerializeField] private Transform shootPoint;
    [SerializeField] private float shootForce = 100f;
    [SerializeField] private int pointsPerKill = 400;
    [SerializeField] private int maxAmmunition = 6;

    private int ammunition;
    private WeaponType currentWeapon = WeaponType.None;

    public bool HasWeapon => currentWeapon == WeaponType.Pistol;
    public bool HasNeedle => currentWeapon == WeaponType.Needle;
    public WeaponType CurrentWeapon => currentWeapon;

    private void Awake()
    {
        ammunition = maxAmmunition;

        if (needleMelee == null)
            needleMelee = GetComponentInChildren<NeedleMelee>(true);
    }

    private void Start()
    {
        UpdateWeaponModels();
    }

    private void UpdateWeaponModels()
    {
        if (NeedleModel != null)
            NeedleModel.SetActive(currentWeapon == WeaponType.Needle);

        if (WeaponModel != null)
            WeaponModel.SetActive(currentWeapon == WeaponType.Pistol);
    }

    public bool CanPickUpNeedle()
    {
        // La pistola tiene prioridad sobre la aguja.
        return currentWeapon != WeaponType.Pistol;
    }

    public bool CanPickUpPistol()
    {
        // No se recoge otra pistola si ya tiene una.
        return currentWeapon != WeaponType.Pistol;
    }

    [PunRPC]
    public void EquipNeedle()
    {
        if (currentWeapon == WeaponType.Pistol)
            return;

        currentWeapon = WeaponType.Needle;
        UpdateWeaponModels();
    }

    [PunRPC]
    public void ShowWeapon()
    {
        currentWeapon = WeaponType.Pistol;
        UpdateWeaponModels();
    }

    private void Update()
    {
        if (!photonView.IsMine)
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        switch (currentWeapon)
        {
            case WeaponType.Needle:
                if (needleMelee != null)
                    needleMelee.TryAttack();
                break;

            case WeaponType.Pistol:
                Shoot();
                break;

            case WeaponType.None:
                // Sin arma no puede atacar.
                break;
        }
    }

    private void Shoot()
    {
        if (ammunition <= 0 || DartPrefab == null || shootPoint == null)
            return;

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

        ammunition--;
    }
}
