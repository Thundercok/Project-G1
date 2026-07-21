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

    void Start()
    {
        pistol = GetComponentInChildren<G1Pistol>(true);
        smg = GetComponentInChildren<G1Smg>(true);
        shotgun = GetComponentInChildren<G1Shotgun>(true);
        magnum = GetComponentInChildren<G1Magnum>(true);
        grenade = GetComponentInChildren<G1Grenade>(true);
    }

    void Update()
    {
        if (pistol && pistol.reserve < pistolReserve) pistol.reserve = pistolReserve;
        if (smg && smg.reserve < smgReserve) smg.reserve = smgReserve;
        if (shotgun && shotgun.reserve < shotgunReserve) shotgun.reserve = shotgunReserve;
        if (magnum && magnum.reserve < magnumReserve) magnum.reserve = magnumReserve;
        if (grenade && grenade.count < grenadeCount) grenade.count = grenadeCount;
    }
}
