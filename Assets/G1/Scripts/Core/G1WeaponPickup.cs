using UnityEngine;

public class G1WeaponPickup : MonoBehaviour
{
    public enum WeaponType { Pistol, Smg, Shotgun, Magnum }
    public WeaponType weaponType;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var switcher = other.GetComponentInChildren<WeaponSwitcher>(true);
        if (switcher == null) return;

        // index mapping: 0 = Crowbar, 1 = Pistol, 2 = Smg, 3 = Shotgun, 4 = Magnum
        int idx = (int)weaponType + 1;
        switcher.Unlock(idx);

        var hud = other.GetComponent<PlayerHUD>();
        if (hud != null) hud.ShowWeaponPickup(weaponType.ToString());

        Debug.Log($"🎮 PICKUP: Unlocked weapon {weaponType}!");
        Destroy(gameObject);
    }
}
