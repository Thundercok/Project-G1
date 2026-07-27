using UnityEngine;

/// Infinite ammunition, for as long as god mode is on.
///
/// God mode already says "I am not playing the resource game right now" — but
/// it only removed one of the two resources. Being unkillable and still having
/// to walk back to an ammo crate every thirty seconds is the worst of both:
/// no tension and no freedom. So the two travel together, and turning god mode
/// off hands the economy straight back.
///
/// It tops the clip up rather than only the reserve, so a weapon never drops
/// into a reload animation mid-burst while you are trying to test something.
public sealed class G1GodModeAmmo : MonoBehaviour
{
    [Header("Reserves held while active")]
    public int pistolReserve = 250;
    public int smgReserve = 300;
    public int shotgunReserve = 64;
    public int magnumReserve = 36;
    public int grenadeCount = 10;

    [Tooltip("Off = always on (the testing range). On = only while god mode is.")]
    public bool requireGodMode = true;

    G1Pistol pistol;
    G1Smg smg;
    G1Shotgun shotgun;
    G1Magnum magnum;
    G1Grenade grenade;
    HealthSystem health;
    WeaponSwitcher switcher;
    bool wasActive;

    /// Read by the HUD, which shows an infinity glyph instead of a count.
    public static bool Unlimited { get; private set; }

    void Start()
    {
        // weapons are held under the camera and the inactive ones are disabled,
        // so this has to search including inactive children
        pistol = GetComponentInChildren<G1Pistol>(true);
        smg = GetComponentInChildren<G1Smg>(true);
        shotgun = GetComponentInChildren<G1Shotgun>(true);
        magnum = GetComponentInChildren<G1Magnum>(true);
        grenade = GetComponentInChildren<G1Grenade>(true);
        health = GetComponent<HealthSystem>();
        switcher = GetComponentInChildren<WeaponSwitcher>(true);
    }

    void OnDisable() { Unlimited = false; }

    void Update()
    {
        bool active = !requireGodMode || (health != null && health.godMode);
        Unlimited = active;

        if (active != wasActive)
        {
            wasActive = active;
            G1Audio.Play2D("pickup", 0.55f, active ? 1.5f : 0.8f);
            Debug.Log($"[GOD MODE] Ammunition {(active ? "UNLIMITED" : "back to normal")}.");
        }
        if (!active) return;

        // Topping up ammo for a weapon you cannot select is not "infinite ammo
        // for every weapon" — it is infinite ammo for the two you happen to
        // have found. God mode hands over the whole rack.
        if (switcher != null && switcher.unlocked != null)
            for (int i = 0; i < switcher.unlocked.Length; i++)
                switcher.unlocked[i] = true;

        if (pistol) { pistol.clip = pistol.clipSize; pistol.reserve = pistolReserve; }
        if (smg) { smg.clip = smg.clipSize; smg.reserve = smgReserve; }
        if (shotgun) { shotgun.clip = shotgun.clipSize; shotgun.reserve = shotgunReserve; }
        if (magnum) { magnum.clip = magnum.clipSize; magnum.reserve = magnumReserve; }
        if (grenade) { grenade.count = grenadeCount; }
    }
}
