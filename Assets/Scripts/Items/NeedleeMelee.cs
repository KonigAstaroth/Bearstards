
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

public class NeedleMelee : MonoBehaviourPun
{
    [Header("Referencias")]
    [SerializeField] private PhotonView playerView;
    [SerializeField] private Collider hitbox;

    [Header("Combate")]
    [SerializeField] private int damage = 25;
    [SerializeField] private int pointsPerKill = 200;
    [SerializeField] private float attackDuration = 0.2f;
    [SerializeField] private float attackCooldown = 0.6f;

    [Header("Animación")]
    [SerializeField] private Animator animator;
    [SerializeField] private string attackStateName = "slash";
    [SerializeField] private float crossFadeDuration = 0.05f;

    private int upperBodyLayerIndex = -1;

    private bool isAttacking;
    private float nextAttackTime;

    // Evita golpear varias veces al mismo jugador durante un ataque.
    private readonly HashSet<int> hitPlayers = new HashSet<int>();

    private void Awake()
    {
        if (playerView == null)
            playerView = GetComponentInParent<PhotonView>();

        if (hitbox != null)
            hitbox.enabled = false;

        if (animator == null)
            animator = GetComponentInParent<Animator>();

        if (animator != null)
            upperBodyLayerIndex = animator.GetLayerIndex("UpperBody");
    }

    private void Update()
    {
        if (playerView == null || !playerView.IsMine)
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

        if (isAttacking || Time.time < nextAttackTime)
            return;

        if (animator == null)
        {
            Debug.LogError("NeedleMelee: no hay Animator asignado.");
            return;
        }

        if (upperBodyLayerIndex < 0)
        {
            Debug.LogError("NeedleMelee: no se encontró la capa UpperBody.");
            return;
        }

        Debug.Log(
            $"Ataque iniciado. Estado: {attackStateName}, " +
            $"Capa: {upperBodyLayerIndex}"
        );

        animator.Play("slash", upperBodyLayerIndex, 0f);

        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        nextAttackTime = Time.time + attackCooldown;
        hitPlayers.Clear();

        if (hitbox != null)
            hitbox.enabled = true;

        yield return new WaitForSeconds(attackDuration);

        if (hitbox != null)
            hitbox.enabled = false;

        isAttacking = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isAttacking || playerView == null || !playerView.IsMine)
            return;

        PhotonView targetView = other.GetComponentInParent<PhotonView>();

        if (targetView == null || targetView == playerView)
            return;

        if (!hitPlayers.Add(targetView.ViewID))
            return;

        HealthSystem health =
            targetView.GetComponentInChildren<HealthSystem>();

        if (health == null)
            return;

        // El daño se procesa en el cliente propietario de la víctima.
        health.photonView.RPC(
            nameof(HealthSystem.TakeDamage),
            health.photonView.Owner,
            damage
        );
    }

    private void OnDisable()
    {
        if (hitbox != null)
            hitbox.enabled = false;

        isAttacking = false;
    }
}
