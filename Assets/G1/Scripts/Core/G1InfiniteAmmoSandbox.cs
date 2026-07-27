using UnityEngine;

/// Testing-range only: switches god mode on at spawn and never lets it off,
/// so the range is a place to practice rather than a place to survive.
///
/// The ammunition itself is <see cref="G1GodModeAmmo"/>, which every player
/// carries — here it is simply told to run unconditionally instead of
/// following the god-mode toggle.
public sealed class G1InfiniteAmmoSandbox : MonoBehaviour
{
    public int pistolReserve = 250;
    public int smgReserve = 300;
    public int shotgunReserve = 64;
    public int magnumReserve = 36;
    public int grenadeCount = 10;

    void Start()
    {
        var health = GetComponent<HealthSystem>();
        if (health != null)
        {
            health.godMode = true;
            health.Heal(health.maxHealth);
            Debug.Log("[SANDBOX] God Mode ON — player cannot die in Weapon Testing Range.");
        }

        var ammo = GetComponent<G1GodModeAmmo>();
        if (ammo == null) ammo = gameObject.AddComponent<G1GodModeAmmo>();
        ammo.requireGodMode = false;           // the range is always stocked
        ammo.pistolReserve = pistolReserve;
        ammo.smgReserve = smgReserve;
        ammo.shotgunReserve = shotgunReserve;
        ammo.magnumReserve = magnumReserve;
        ammo.grenadeCount = grenadeCount;
    }
}
