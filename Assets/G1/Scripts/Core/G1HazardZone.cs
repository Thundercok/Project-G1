using UnityEngine;

public class G1HazardZone : MonoBehaviour
{
    public float damagePerSecond = 12f;
    private float nextDamageTime;

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (Time.time >= nextDamageTime)
            {
                nextDamageTime = Time.time + 0.6f;
                var hp = other.GetComponent<HealthSystem>();
                if (hp != null)
                {
                    hp.TakeDamage(damagePerSecond * 0.6f, other.transform.position + Vector3.up, Vector3.up);
                    // Pitch up hit_thunk to sound like a clicking Geiger Counter warning
                    G1Audio.Play2D("hit_thunk", 0.35f, 2.3f);
                }
            }
        }
    }
}
