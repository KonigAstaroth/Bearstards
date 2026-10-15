
using UnityEngine;
using Photon.Pun;
using UnityEngine.UI;

public class HealthSystem : MonoBehaviourPun
{
    [Header("Health")]
    [SerializeField] private Canvas playerCanvas;
    [SerializeField] private Image healthBar;
    [SerializeField] private int maxHealth = 100;

    private int currentHealth;
    private bool isDead;

    private void Awake()
    {
        currentHealth = maxHealth;
        isDead = false;
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
    public void TakeDamage( int damage, int attackerViewID, int pointsPerKill)
    {
        if (!photonView.IsMine || isDead)
            return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
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
        if (!photonView.IsMine || isDead)
            return;

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        UpdateHealth();
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

    public bool HasFullHealth()
    {
        return currentHealth >= maxHealth;
    }
}
