using UnityEngine;
public class DamageCube : MonoBehaviour 
{
    [SerializeField] private int damage = 25; 
    private void OnTriggerEnter(Collider other) 
    { 
        HealthSystem health = other.GetComponent<HealthSystem>(); 
        if (health != null) 
        { 
            health.TakeDamage(damage); 
        } 
    } 
}