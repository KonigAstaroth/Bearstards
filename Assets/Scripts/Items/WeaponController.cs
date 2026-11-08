
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
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float shootForce = 100f;
    [SerializeField] private float aimDistance = 100f;
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
        return currentWeapon != WeaponType.Pistol;
    }

    public bool CanPickUpPistol()
    {
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
        }
    }

    private void Shoot()
    {
        if (ammunition <= 0 || DartPrefab == null ||
            shootPoint == null || playerCamera == null)
            return;

        // Raycast desde el centro de la pantalla.
        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        Vector3 targetPoint = ray.GetPoint(aimDistance);

        // Buscar el impacto más cercano ignorando al propio jugador.
        RaycastHit[] hits = Physics.RaycastAll(ray, aimDistance);

        float closestDistance = float.MaxValue;

        foreach (RaycastHit hit in hits)
        {
            PhotonView hitView =
                hit.collider.GetComponentInParent<PhotonView>();

            // Ignorar los colliders del jugador que está disparando.
            if (hitView == photonView)
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                targetPoint = hit.point;
            }
        }

        // Dirección desde la pistola hasta el punto de mira.
        Vector3 shootDirection =
            (targetPoint - shootPoint.position).normalized;

        // Instanciar proyectil mediante Photon.
        GameObject dart = PhotonNetwork.Instantiate(
            DartPrefab.name,
            shootPoint.position,
            Quaternion.LookRotation(shootDirection),
            0,
            new object[]
            {
                photonView.ViewID,
                pointsPerKill
            }
        );

        // Evitar colisiones con el propio jugador.
        Collider[] playerColliders =
            photonView.GetComponentsInChildren<Collider>();

        Collider[] dartColliders =
            dart.GetComponentsInChildren<Collider>();

        foreach (Collider playerCollider in playerColliders)
        {
            foreach (Collider dartCollider in dartColliders)
            {
                if (playerCollider != null && dartCollider != null)
                {
                    Physics.IgnoreCollision(
                        playerCollider,
                        dartCollider
                    );
                }
            }
        }

        Rigidbody rb = dart.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.collisionDetectionMode =
                CollisionDetectionMode.ContinuousDynamic;

            rb.AddForce(
                shootDirection * shootForce,
                ForceMode.Impulse
            );
        }

        ammunition--;
    }
}
