using UnityEngine;

/// Testing-range only: keeps every weapon's reserve topped up and grenades
/// full so the player never runs dry while practicing. Attach to the player.
public sealed class G1InfiniteAmmoSandbox : MonoBehaviour
{
    public int pistolReserve = 250;
    public int smgReserve = 300;
    public int shotgunReserve = 64;
    public int magnumReserve = 36;
    public int grenadeCount = 10;

    G1Pistol pistol;
    G1Smg smg;
    G1Shotgun shotgun;
    G1Magnum magnum;
    G1Grenade grenade;
    HealthSystem health;

    void Start()
    {
        pistol   = GetComponentInChildren<G1Pistol>(true);
        smg      = GetComponentInChildren<G1Smg>(true);
        shotgun  = GetComponentInChildren<G1Shotgun>(true);
        magnum   = GetComponentInChildren<G1Magnum>(true);
        grenade  = GetComponentInChildren<G1Grenade>(true);
        health   = GetComponent<HealthSystem>();

        // Make the player unkillable in the test range
        if (health != null)
        {
            health.godMode = true;
            health.Heal(health.maxHealth);
            Debug.Log("[SANDBOX] God Mode ON — player cannot die in Weapon Testing Range.");
        }
    }

    void Update()
    {
        // Lock clip to full so weapons never trigger a reload mid-fire
        if (pistol)  { pistol.clip  = pistol.clipSize;   pistol.reserve  = pistolReserve; }
        if (smg)     { smg.clip     = smg.clipSize;       smg.reserve     = smgReserve; }
        if (shotgun) { shotgun.clip = shotgun.clipSize;   shotgun.reserve = shotgunReserve; }
        if (magnum)  { magnum.clip  = magnum.clipSize;    magnum.reserve  = magnumReserve; }
        if (grenade) { grenade.count = grenadeCount; }
    }
}
