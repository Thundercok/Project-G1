using UnityEngine;

public class G1WeaponPickup : MonoBehaviour
{
    public enum WeaponType { Pistol, Smg, Shotgun, Magnum, Grenade }
    public WeaponType weaponType;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var switcher = other.GetComponentInChildren<WeaponSwitcher>(true);
        if (switcher == null) return;

        // index mapping: 0 = Crowbar, 1 = Pistol, 2 = Smg, 3 = Shotgun, 4 = Magnum
        int idx = (int)weaponType + 1;
        switcher.Unlock(idx);
        if (weaponType == WeaponType.Grenade && idx < switcher.weapons.Length)
        {
            var g = switcher.weapons[idx].GetComponent<G1Grenade>();
            if (g)
                g.count = Mathf.Min(g.count + 3, g.maxCount);
        }

        var hud = other.GetComponent<PlayerHUD>();
        if (hud != null) hud.ShowWeaponPickup(weaponType.ToString());
        G1Audio.Play2D("pickup", 0.8f);

        Debug.Log($"🎮 PICKUP: Unlocked weapon {weaponType}!");
        Destroy(gameObject);
    }
}
