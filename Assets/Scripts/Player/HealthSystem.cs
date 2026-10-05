using UnityEngine;
using Photon.Pun;
using TMPro;

public class HealthSystem : MonoBehaviourPun
{

    [Header("Health")]
    public TextMeshProUGUI healthText;
    [SerializeField] private int maxHealth = 100;

    private int currentHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

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
        Debug.Log("Vida: " + currentHealth);
        healthText.text = currentHealth.ToString();
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
        Debug.Log("Moriste");
        SpawnPlayers spawnManager = FindAnyObjectByType<SpawnPlayers>();

        if (spawnManager != null)
        {
            spawnManager.RespawnPlayer(gameObject);
        }
    }

}
