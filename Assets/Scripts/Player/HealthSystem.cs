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

    private void Start()
    {
        if (!photonView.IsMine)
        {
            playerCanvas.enabled = false;
            return;
        }

        updateHealth();
    }

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    [PunRPC]
    public void TakeDamage(int _damage)
    {

        if (!photonView.IsMine)
        {
            return;
        }

        currentHealth -= _damage;
        updateHealth();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void updateHealth()
    {
        if (healthBar != null)
        {
            healthBar.fillAmount = Mathf.Clamp01((float)currentHealth / maxHealth);
        }
    }

    public void Heal(int amount)
    {
        if (!photonView.IsMine)
        {
            return;
        }

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        updateHealth();
    }

    private void Die()
    {
        SpawnPlayers spawnManager = FindAnyObjectByType<SpawnPlayers>();

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
