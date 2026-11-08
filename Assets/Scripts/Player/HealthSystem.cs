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

    [Header("NPC")]
    [Tooltip("Márcalo en los NPCs: al morir avisa con el evento Died en lugar de reaparecer como jugador.")]
    [SerializeField] private bool isNpc = false;
    [Tooltip("Puntos que pierde el jugador que mata a este NPC.")]
    [SerializeField] private int npcKillPenalty = 100;

    /// <summary>Se dispara al morir cuando isNpc está activo.</summary>
    public event System.Action Died;

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
            $"{(isNpc ? "NPC" : "PLAYER")} DIED | Attacker ID: {attackerViewID} | " +
            $"Points: {pointsPerKill}"
        );

        // Los NPCs no dan puntos: le restan al jugador que los mata.
        // Se maneja aparte porque el NPC pertenece al MasterClient, y la comprobación
        // de abajo (atacante distinto del dueño) dejaría sin castigo al jugador master.
        if (isNpc)
        {
            ApplyNpcKillPenalty(attackerViewID);
            Died?.Invoke();
            return;
        }

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

    private void ApplyNpcKillPenalty(int attackerViewID)
    {
        if (npcKillPenalty <= 0)
            return;

        PhotonView attackerView = PhotonView.Find(attackerViewID);

        if (attackerView == null || attackerView.Owner == null)
            return;

        // AwardKillPoints suma el valor recibido, así que un número negativo resta
        attackerView.RPC(
            nameof(ScoreManager.AwardKillPoints),
            attackerView.Owner,
            -npcKillPenalty
        );
    }
}
