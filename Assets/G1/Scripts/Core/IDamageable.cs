using UnityEngine;

/// Anything that can be hurt: crates, NPCs, later the player.
public interface IDamageable
{
    void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitNormal);
}
