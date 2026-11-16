
using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;
using UnityEngine.UI;
using TMPro;
using System.Collections;

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
    [SerializeField] private Light muzzleLight;

    [Header ("HUD")]
    [SerializeField] private TMP_Text dartCounter;
    [SerializeField] private Image dartSprite;

    [Header ("SFX")]
    [SerializeField] private PlayerAudio playerAudio;

    private int ammunition;
    private WeaponType currentWeapon = WeaponType.None;

    // La aguja se puede guardar/sacar con la tecla E sin perderla.
    private bool needleHidden;

    public bool HasWeapon => currentWeapon == WeaponType.Pistol;
    public bool HasNeedle => currentWeapon == WeaponType.Needle;
    public bool IsNeedleHidden => needleHidden;
    public bool CanUseNeedle => currentWeapon == WeaponType.Needle && !needleHidden;
    // true si el jugador trae algo en la mano (para la capa UpperBody del Animator).
    public bool IsHoldingWeapon =>
        currentWeapon == WeaponType.Pistol || CanUseNeedle;
    public WeaponType CurrentWeapon => currentWeapon;

    private void Awake()
    {
        ammunition = maxAmmunition;

        if (needleMelee == null)
            needleMelee = GetComponentInChildren<NeedleMelee>(true);
        
        muzzleLight.enabled = false;
    }

    private void Start()
    {
        UpdateWeaponModels();
        UpdateAmmoHUD();
    }

    private void UpdateWeaponModels()
    {
        if (NeedleModel != null)
            NeedleModel.SetActive(CanUseNeedle);

        if (WeaponModel != null)
            WeaponModel.SetActive(currentWeapon == WeaponType.Pistol);
    }

    private void UpdateAmmoHUD()
    {
        if(!photonView.IsMine) return;

        bool showHUD = currentWeapon == WeaponType.Pistol;

        if (dartCounter != null && dartSprite != null)
        {
            dartCounter.gameObject.SetActive(showHUD);
            dartSprite.gameObject.SetActive(showHUD);
            dartCounter.text = ammunition.ToString() + "/" + maxAmmunition;
            dartCounter.color = ammunition <=3 ? Color.red : Color.white;
            
        }

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
        // Al recoger una aguja siempre aparece en la mano.
        needleHidden = false;
        UpdateWeaponModels();
    }

    [PunRPC]
    public void ShowWeapon()
    {
        currentWeapon = WeaponType.Pistol;
        needleHidden = false;
        UpdateWeaponModels();
        UpdateAmmoHUD();
    }

    [PunRPC]
    public void RemovePistol()
    {
        if (currentWeapon != WeaponType.Pistol) return;

        currentWeapon = WeaponType.None;
        needleHidden = false;

        UpdateWeaponModels();
        UpdateAmmoHUD();
    }

    [PunRPC]
    public void SetNeedleHidden(bool hidden)
    {
        if (currentWeapon != WeaponType.Needle)
            return;

        needleHidden = hidden;
        UpdateWeaponModels();
    }

    private void Update()
    {
        if (!photonView.IsMine || MenuPausa.Abierto)
            return;

        // E: guardar / sacar la aguja.
        if (currentWeapon == WeaponType.Needle &&
            Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            photonView.RPC(
                nameof(SetNeedleHidden),
                RpcTarget.All,
                !needleHidden
            );
        }

        if (!Input.GetMouseButtonDown(0))
            return;

        switch (currentWeapon)
        {
            case WeaponType.Needle:
                if (needleMelee != null && !needleHidden)
                    needleMelee.TryAttack();
                break;

            case WeaponType.Pistol:
                Shoot();
                break;
        }
    }

    IEnumerator TriggerMuzzleLight()
    {
        muzzleLight.enabled = true;
        yield return new WaitForSecondsRealtime(0.04f);
        muzzleLight.enabled = false;
    }

    private void Shoot()
    {
        if (ammunition <=0 || DartPrefab == null ||
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

        if (muzzleLight != null) StartCoroutine(TriggerMuzzleLight());

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
        playerAudio.PlayShoot();
        ammunition--;
        UpdateAmmoHUD();

        if(ammunition <= 0)
        {
            photonView.RPC(nameof(RemovePistol), RpcTarget.All);
        }
    }
}
