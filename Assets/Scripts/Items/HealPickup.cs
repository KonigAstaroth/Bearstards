using UnityEngine;

public class HealOnCollision : MonoBehaviour
{
    [SerializeField] private int healPoints = 25;
    private void OnTriggerEnter(Collider other)
    {
        HealthSystem health = other.GetComponent<HealthSystem>();
        if (health != null)
        {
            health.Heal(healPoints);
            Destroy(gameObject);
        }
    }
}

