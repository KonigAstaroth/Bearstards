
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class OilBarrel : MonoBehaviourPun
{
    [Header("Models")]
    [SerializeField] private GameObject normalModel;
    [SerializeField] private GameObject destroyedModel;

    [Header("Collisions")]
    [SerializeField] private GameObject explosionTrigger;
    [SerializeField] private SphereCollider explosionCollider;

    [Header("FX")]
    [SerializeField] private Light preExplosionLight;
    [SerializeField] private Light explosionLight;
    [SerializeField] private ParticleSystem explosionParticles;
    [SerializeField] private ParticleSystem debrisParticles;

    [Header("Combat")]
    [SerializeField] private int hitsToExplode = 2;
    [SerializeField] private int explosionDamage = 100;
    [SerializeField] private float explosionDuration = .5f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip explosionSound;
    [SerializeField] private float explosionVolume = .8f;

    private int hitCount;
    private bool exploded;
    private bool preExplosion;

    private Coroutine explosionRoutine;
    private Coroutine preExplosionRoutine;

    private readonly HashSet<int> damagedPlayers = new();

    private void Awake()
    {
        if (normalModel != null)
            normalModel.SetActive(true);

        if (destroyedModel != null)
            destroyedModel.SetActive(false);

        if (preExplosionLight != null)
            preExplosionLight.enabled = false;

        if (explosionLight != null)
            explosionLight.enabled = false;

        if (explosionTrigger != null)
            explosionTrigger.SetActive(false);

        if (explosionCollider != null)
            explosionCollider.enabled = false;
    }

    [PunRPC]
    public void RegisterHit()
    {
        if (!PhotonNetwork.IsMasterClient || exploded)
            return;

        hitCount++;

        Debug.Log($"BARREL HIT: {hitCount}/{hitsToExplode}");

        if (hitCount == 1)
        {
            photonView.RPC(nameof(StartPreExplosion), RpcTarget.All);
        }

        if (hitCount >= hitsToExplode)
        {
            photonView.RPC(nameof(Explode), RpcTarget.All);
        }
    }

    [PunRPC]
    private void StartPreExplosion()
    {
        if (preExplosion || exploded)
            return;

        Debug.Log($"PRE-EXPLOSION RPC | {name}");

        preExplosion = true;
        preExplosionRoutine = StartCoroutine(PreExplosionRoutine());
    }

    private IEnumerator PreExplosionRoutine()
    {
        while (preExplosion && !exploded)
        {
            if (preExplosionLight != null)
                preExplosionLight.enabled = true;
            Debug.Log(
                $"RED LIGHT | Enabled: {preExplosionLight.enabled} | " +
                $"Active: {preExplosionLight.gameObject.activeInHierarchy} | " +
                $"Intensity: {preExplosionLight.intensity} | " +
                $"Range: {preExplosionLight.range}"
            );

            yield return new WaitForSeconds(0.5f);

            if (preExplosionLight != null)
                preExplosionLight.enabled = false;

            yield return new WaitForSeconds(0.5f);
        }

        if (preExplosionLight != null)
            preExplosionLight.enabled = false;

        preExplosionRoutine = null;
    }

    [PunRPC]
    private void Explode()
    {
        if (exploded)
            return;

        exploded = true;
        preExplosion = false;

        if (preExplosionRoutine != null)
        {
            StopCoroutine(preExplosionRoutine);
            preExplosionRoutine = null;
        }

        damagedPlayers.Clear();

        if (normalModel != null)
            normalModel.SetActive(false);

        if (destroyedModel != null)
            destroyedModel.SetActive(true);

        if (preExplosionLight != null)
            preExplosionLight.enabled = false;

        if (explosionLight != null)
            explosionLight.enabled = true;

        if (explosionParticles != null)
        {
            explosionParticles.gameObject.SetActive(true);
            explosionParticles.Play();
        }

        if (debrisParticles != null)
        {
            debrisParticles.gameObject.SetActive(true);
            debrisParticles.Play();
        }

        if (explosionTrigger != null)
            explosionTrigger.SetActive(true);

        if (explosionCollider != null)
            explosionCollider.enabled = true;

        if (audioSource != null && explosionSound != null)
        {
            audioSource.PlayOneShot(explosionSound, explosionVolume);
        }

        explosionRoutine = StartCoroutine(ExplosionRoutine());
    }

    private IEnumerator ExplosionRoutine()
    {
        yield return new WaitForSeconds(explosionDuration);

        if (explosionTrigger != null)
            explosionTrigger.SetActive(false);

        if (explosionCollider != null)
            explosionCollider.enabled = false;

        if (explosionLight != null)
            explosionLight.enabled = false;

        if (explosionParticles != null && debrisParticles != null)
        {
            explosionParticles.gameObject.SetActive(false);
            debrisParticles.gameObject.SetActive(false);
        }

        explosionRoutine = null;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!PhotonNetwork.IsMasterClient || !exploded)
            return;
        

        HealthSystem health = other.GetComponentInParent<HealthSystem>();

        if (health == null)
            return;

        PhotonView playerView = health.photonView;

        if (playerView == null || playerView.Owner == null)
            return;

        int actorNumber = playerView.OwnerActorNr;

        if (!damagedPlayers.Add(actorNumber))
            return;

        playerView.RPC(
            nameof(HealthSystem.TakeDamage),
            RpcTarget.All,
            explosionDamage,
            -1,
            0
        );
    }
}
