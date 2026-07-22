using UnityEngine;

/// Attached to the Player GameObject.
/// On Start(), checks if a level clear transition or campaign save exists,
/// and restores the player's health, unlocked weapons, ammo, and grenades.
public class G1PlayerInventoryRestorer : MonoBehaviour
{
    void Start()
    {
        // Checkpoint restorer takes precedence if respawning after death
        if (G1Checkpoint.HasSave && G1Checkpoint.ConsumeRestorePending())
            return;

        if (!G1SaveSystem.HasSaveData())
            return;

        var data = G1SaveSystem.Load();
        if (!data.isLevelClearTransition)
            return;

        // Apply health
        var health = GetComponent<HealthSystem>();
        if (health != null && data.health > 0)
        {
            health.Heal(Mathf.Max(50f, data.health) - health.CurrentHealth);
        }

        // Apply unlocked weapons and ammo
        var switcher = GetComponentInChildren<WeaponSwitcher>(true);
        if (switcher != null && switcher.unlocked != null)
        {
            for (int i = 0; i < switcher.unlocked.Length; i++)
            {
                if ((data.unlockMask & (1 << i)) != 0)
                    switcher.unlocked[i] = true;
            }
            switcher.unlocked[0] = true; // Pistol is always unlocked

            foreach (var w in switcher.weapons)
            {
                if (w == null) continue;
                if (w.TryGetComponent(out G1Pistol p))
                {
                    if (data.clips.Length > 0 && data.clips[0] > 0) p.clip = data.clips[0];
                    if (data.reserves.Length > 0 && data.reserves[0] > 0) p.reserve = data.reserves[0];
                }
                else if (w.TryGetComponent(out G1Smg s))
                {
                    if (data.clips.Length > 1 && data.clips[1] > 0) s.clip = data.clips[1];
                    if (data.reserves.Length > 1 && data.reserves[1] > 0) s.reserve = data.reserves[1];
                }
                else if (w.TryGetComponent(out G1Shotgun sh))
                {
                    if (data.clips.Length > 2 && data.clips[2] > 0) sh.clip = data.clips[2];
                    if (data.reserves.Length > 2 && data.reserves[2] > 0) sh.reserve = data.reserves[2];
                }
                else if (w.TryGetComponent(out G1Magnum m))
                {
                    if (data.clips.Length > 3 && data.clips[3] > 0) m.clip = data.clips[3];
                    if (data.reserves.Length > 3 && data.reserves[3] > 0) m.reserve = data.reserves[3];
                }
                else if (w.TryGetComponent(out G1Grenade g))
                {
                    if (data.grenades > 0) g.count = data.grenades;
                }
            }
        }

        Debug.Log("[G1PlayerInventoryRestorer] Inventory restored from campaign save.");
    }
}
