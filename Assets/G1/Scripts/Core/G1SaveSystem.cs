using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Cross-session save to persistentDataPath/savegame.json. Written at every
/// checkpoint; loaded by the main-menu "Continue" item. Separate from the
/// in-level PlayerPrefs death-checkpoint (which handles same-session respawn).
public static class G1SaveSystem
{
    [System.Serializable]
    public struct SaveData
    {
        public string scene;
        public float x, y, z, yaw;
        public float health, armor;
        public int unlockMask, grenades;
        public int[] clips;      // pistol, smg, shotgun, magnum
        public int[] reserves;
    }

    static string Path => System.IO.Path.Combine(
        Application.persistentDataPath, "savegame.json");
    const string ContinueFlag = "G1_ContinuePending";

    public static bool HasSave => File.Exists(Path);

    public static void SaveFromPlayer(GameObject player)
    {
        var d = new SaveData
        {
            scene = SceneManager.GetActiveScene().name,
            x = player.transform.position.x,
            y = player.transform.position.y,
            z = player.transform.position.z,
            yaw = player.transform.eulerAngles.y,
            clips = new int[4],
            reserves = new int[4],
        };
        var hs = player.GetComponent<HealthSystem>();
        if (hs) { d.health = hs.CurrentHealth; d.armor = hs.Armor; }

        var switcher = player.GetComponentInChildren<WeaponSwitcher>(true);
        if (switcher != null && switcher.unlocked != null)
        {
            for (int i = 0; i < switcher.unlocked.Length; i++)
                if (switcher.unlocked[i]) d.unlockMask |= 1 << i;
            foreach (var w in switcher.weapons)
            {
                if (w.TryGetComponent(out G1Pistol p)) { d.clips[0] = p.clip; d.reserves[0] = p.reserve; }
                else if (w.TryGetComponent(out G1Smg s)) { d.clips[1] = s.clip; d.reserves[1] = s.reserve; }
                else if (w.TryGetComponent(out G1Shotgun sh)) { d.clips[2] = sh.clip; d.reserves[2] = sh.reserve; }
                else if (w.TryGetComponent(out G1Magnum m)) { d.clips[3] = m.clip; d.reserves[3] = m.reserve; }
                else if (w.TryGetComponent(out G1Grenade g)) { d.grenades = g.count; }
            }
        }

        try
        {
            File.WriteAllText(Path, JsonUtility.ToJson(d, true));
            Debug.Log("Game saved: " + Path);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Save failed: " + e.Message);
        }
    }

    public static SaveData Load() => JsonUtility.FromJson<SaveData>(File.ReadAllText(Path));

    /// Menu "Continue": mark a restore pending and load the saved scene.
    public static void Continue()
    {
        if (!HasSave)
            return;
        var d = Load();
        PlayerPrefs.SetInt(ContinueFlag, 1);
        PlayerPrefs.Save();
        SceneManager.LoadScene(string.IsNullOrEmpty(d.scene) ? "TestScene" : d.scene);
    }

    public static bool ConsumeContinuePending()
    {
        if (PlayerPrefs.GetInt(ContinueFlag, 0) != 1)
            return false;
        PlayerPrefs.SetInt(ContinueFlag, 0);
        return true;
    }
}

/// On the player: if a Continue is pending, apply the on-disk save after the
/// scene builds. Runs after G1CheckpointRestorer so an explicit Continue wins.
public sealed class G1SaveApplier : MonoBehaviour
{
    void Start()
    {
        if (!G1SaveSystem.ConsumeContinuePending() || !G1SaveSystem.HasSave)
            return;
        var d = G1SaveSystem.Load();

        var cc = GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        transform.position = new Vector3(d.x, d.y, d.z);
        transform.rotation = Quaternion.Euler(0f, d.yaw, 0f);
        if (cc) cc.enabled = true;

        var hs = GetComponent<HealthSystem>();
        if (hs) hs.SetState(d.health, d.armor);

        var switcher = GetComponentInChildren<WeaponSwitcher>(true);
        if (switcher != null && switcher.unlocked != null)
        {
            for (int i = 0; i < switcher.unlocked.Length; i++)
                switcher.unlocked[i] = (d.unlockMask & (1 << i)) != 0;
            switcher.unlocked[0] = true;
            foreach (var w in switcher.weapons)
            {
                if (w.TryGetComponent(out G1Pistol p)) { p.clip = d.clips[0]; p.reserve = d.reserves[0]; }
                else if (w.TryGetComponent(out G1Smg s)) { s.clip = d.clips[1]; s.reserve = d.reserves[1]; }
                else if (w.TryGetComponent(out G1Shotgun sh)) { sh.clip = d.clips[2]; sh.reserve = d.reserves[2]; }
                else if (w.TryGetComponent(out G1Magnum m)) { m.clip = d.clips[3]; m.reserve = d.reserves[3]; }
                else if (w.TryGetComponent(out G1Grenade g)) { g.count = d.grenades; }
            }
        }
        Debug.Log("Continue: save restored");
    }
}
