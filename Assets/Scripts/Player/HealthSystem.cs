
using UnityEngine;
using Photon.Pun;
using UnityEngine.UI;

public class HealthSystem : MonoBehaviourPun
{
    [Header("Health")]
    [SerializeField] private Canvas playerCanvas;
    [SerializeField] private Image healthBar;
    [SerializeField] private int maxHealth = 100;

    [Header("Shield")]
    [SerializeField] private int shieldAmount = 50;
    [SerializeField] private GameObject helmetModel;

    private int currentHealth;
    private int currentShield;
    private bool isDead;

    private void Awake()
    {
        currentHealth = maxHealth;
        currentShield = 0;
        isDead = false;

        if (helmetModel != null)
            helmetModel.SetActive(false);
    }

    private void Start()
    {
        if (!photonView.IsMine)
        {
            if (playerCanvas != null)
                playerCanvas.enabled = false;

            return;
        }

        UpdateHealth();
    }

    [PunRPC]
    public void TakeDamage(int damage, int attackerViewID, int pointsPerKill)
    {
        if (!photonView.IsMine || isDead || damage <= 0)
            return;

        int absorbedDamage = Mathf.Min(currentShield, damage);

        currentShield -= absorbedDamage;
        damage -= absorbedDamage;

        if (damage > 0)
        {
            currentHealth = Mathf.Max(0, currentHealth - damage);
        }

        // 3. Ocultar el casco cuando el escudo se agote.
        if (currentShield <= 0 && helmetModel != null)
        {
            helmetModel.SetActive(false);
        }

        UpdateHealth();

        if (currentHealth <= 0)
        {
            isDead = true;
            Die(attackerViewID, pointsPerKill);
        }
    }

    private void UpdateHealth()
    {
        if (healthBar != null)
        {
            healthBar.fillAmount =
                Mathf.Clamp01((float)currentHealth / maxHealth);
        }
    }

    public void Heal(int amount)
    {
        if (!photonView.IsMine || isDead || amount <= 0)
            return;

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        UpdateHealth();
    }

    public void ActivateShield()
    {
        if (!photonView.IsMine || isDead)
            return;

        currentShield = shieldAmount;

        if (helmetModel != null)
            helmetModel.SetActive(true);
    }

    public int GetCurrentShield()
    {
        return currentShield;
    }

    public bool HasFullHealth()
    {
        return currentHealth >= maxHealth;
    }

    private void Die(int attackerViewID, int pointsPerKill)
    {
        Debug.Log(
            $"PLAYER DIED | Attacker ID: {attackerViewID} | " +
            $"Points: {pointsPerKill}"
        );

        PhotonView attackerView = PhotonView.Find(attackerViewID);

        if (attackerView != null &&
            attackerView.OwnerActorNr != photonView.OwnerActorNr &&
            pointsPerKill > 0)
        {
            attackerView.RPC(
                nameof(ScoreManager.AwardKillPoints),
                attackerView.Owner,
                pointsPerKill
            );
        }

        SpawnPlayers spawnManager =
            FindAnyObjectByType<SpawnPlayers>();

        if (spawnManager != null)
        {
            spawnManager.RespawnPlayer(gameObject);
        }
    }
}
