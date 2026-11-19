
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

public class NeedleMelee : MonoBehaviourPun
{
    [Header("References")]
    [SerializeField] private PhotonView playerView;
    [SerializeField] private Collider hitbox;

    [Header("Combat")]
    [SerializeField] private int damage = 25;
    [SerializeField] private int pointsPerKill = 200;
    [SerializeField] private float attackDuration = 1f;
    [SerializeField] private float attackCooldown = 0.6f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string attackStateName = "slash";
    [SerializeField] private float crossFadeDuration = 0.05f;

    private int upperBodyLayerIndex = -1;
    private WeaponController weaponController;

    private bool isAttacking;
    private float nextAttackTime;

    // Evita golpear varias veces al mismo jugador durante un ataque.
    private readonly HashSet<int> hitPlayers = new HashSet<int>();
    private readonly HashSet<int> hitBarrels = new();

    private void Awake()
    {
        if (playerView == null)
            playerView = GetComponentInParent<PhotonView>();

        weaponController = GetComponentInParent<WeaponController>();

        if (hitbox != null)
            hitbox.enabled = false;

        if (animator == null)
            animator = GetComponentInParent<Animator>();

        if (animator != null)
            upperBodyLayerIndex = animator.GetLayerIndex("UpperBody");
    }

    private void Update()
    {
        if (playerView == null || !playerView.IsMine || MenuPausa.Abierto)
            return;

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryAttack();
        }
    }

    public void TryAttack()
    {
        if (playerView == null || !playerView.IsMine)
            return;

        // No atacar si el jugador no tiene la aguja en la mano (guardada con E).
        if (weaponController != null && !weaponController.CanUseNeedle)
            return;

        if (isAttacking || Time.time < nextAttackTime)
            return;

        if (animator == null)
        {
            
            return;
        }

        if (upperBodyLayerIndex < 0)
        {
            
            return;
        }

        PlaySlashAnimation();

        // animator.Play no lo sincroniza PhotonAnimatorView: avisar a los demás.
        playerView.RPC(nameof(WeaponController.PlayNeedleSlash), RpcTarget.Others);

        StartCoroutine(AttackRoutine());
    }

    public void PlaySlashAnimation()
    {
        if (animator == null || upperBodyLayerIndex < 0)
            return;

        animator.Play(attackStateName, upperBodyLayerIndex, 0f);
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        nextAttackTime = Time.time + attackCooldown;
        hitPlayers.Clear();
        hitBarrels.Clear();


        if (hitbox != null)
            hitbox.enabled = true;

        Debug.Log("HITBOX ENABLED: " + hitbox.enabled);

        yield return new WaitForSeconds(attackDuration);

        if (hitbox != null)
            hitbox.enabled = false;

        isAttacking = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isAttacking || playerView == null || !playerView.IsMine) return;

        OilBarrel barrel = other.GetComponentInParent<OilBarrel>();

        if (barrel != null)
        {
            if (!hitBarrels.Add(barrel.photonView.ViewID)) return;
            
            barrel.photonView.RPC(
                nameof(OilBarrel.RegisterHit),
                RpcTarget.MasterClient
            );

            return;
        }

        HealthSystem health = other.GetComponentInParent<HealthSystem>();

        if (health == null) return;

        PhotonView targetView = health.photonView;

        if (targetView == null || targetView == playerView)
            return;

        if (!hitPlayers.Add(targetView.ViewID))
            return;

        Debug.Log("AGUJA: aplicando " + damage + " de daño a " + targetView.Owner.NickName);

        health.photonView.RPC(
            nameof(HealthSystem.TakeDamage),
            health.photonView.Owner,
            damage,
            playerView.ViewID,
            pointsPerKill
        );
    }

    private void OnDisable()
    {
        if (hitbox != null)
            hitbox.enabled = false;

        isAttacking = false;
    }


    // Temporal
    private void OnDrawGizmos()
    {
        if (hitbox == null)
            return;

        Gizmos.color = hitbox.enabled ? Color.green : Color.red;

        if (hitbox is BoxCollider box)
        {
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }

}
