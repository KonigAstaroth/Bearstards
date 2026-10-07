using UnityEngine;
using Photon.Pun;
using TMPro;

public class HealthSystem : MonoBehaviourPun
{

    [Header("Health")]
    [SerializeField] private Canvas playerCanvas;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private int maxHealth = 100;

    private int currentHealth;

    private void Start()
    {

        Debug.Log(
            "Owner: " + photonView.IsMine +
            " | Player: " + photonView.ViewID + 
            " | Canvas: " + playerCanvas.name
        );
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
