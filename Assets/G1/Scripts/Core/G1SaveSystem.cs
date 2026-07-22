using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Cross-session save system (savegame.json). Stores scene, coordinates, health, armor,
/// weapon unlocks, ammo, and campaign progress across levels and sessions.
public static class G1SaveSystem
{
    [System.Serializable]
    public class SaveData
    {
        public string scene = "TestScene";
        public string currentScene = "TestScene";
        public int maxUnlockedLevelIndex = 1;
        public float x, y, z, yaw;
        public float health = 100f, armor = 0f;
        public int unlockMask = 1, grenades = 0;
        public int[] clips = new int[4];      // pistol, smg, shotgun, magnum
        public int[] reserves = new int[4];
        public bool isLevelClearTransition = false;

        public SaveData()
        {
            clips = new int[4];
            reserves = new int[4];
        }
    }

    static string Path => System.IO.Path.Combine(
        Application.persistentDataPath, "savegame.json");
    const string ContinueFlag = "G1_ContinuePending";

    public static bool HasSave => File.Exists(Path);

    public static bool HasSaveData() => HasSave;

    public static SaveData Load()
    {
        if (!HasSave)
            return new SaveData();

        try
        {
            string json = File.ReadAllText(Path);
            var d = JsonUtility.FromJson<SaveData>(json);
            if (d == null) d = new SaveData();
            if (d.clips == null || d.clips.Length < 4) d.clips = new int[4];
            if (d.reserves == null || d.reserves.Length < 4) d.reserves = new int[4];
            if (string.IsNullOrEmpty(d.currentScene)) d.currentScene = d.scene;
            return d;
        }
        catch
        {
            return new SaveData();
        }
    }

    public static void Save(SaveData data)
    {
        if (data == null) return;
        try
        {
            data.scene = data.currentScene;
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(Path, json);
            Debug.Log($"[G1SaveSystem] Campaign saved to {Path}. Level: {data.currentScene}, Unlocked: {data.maxUnlockedLevelIndex}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Save failed: " + e.Message);
        }
    }

    public static void SaveFromPlayer(GameObject player)
    {
        if (player == null) return;
        var data = Load();
        data.currentScene = SceneManager.GetActiveScene().name;
        data.scene = data.currentScene;
        data.x = player.transform.position.x;
        data.y = player.transform.position.y;
        data.z = player.transform.position.z;
        data.yaw = player.transform.eulerAngles.y;

        var hs = player.GetComponent<HealthSystem>();
        if (hs) { data.health = hs.CurrentHealth; data.armor = hs.Armor; }

        var switcher = player.GetComponentInChildren<WeaponSwitcher>(true);
        if (switcher != null && switcher.unlocked != null)
        {
            data.unlockMask = 0;
            for (int i = 0; i < switcher.unlocked.Length; i++)
                if (switcher.unlocked[i]) data.unlockMask |= 1 << i;
            foreach (var w in switcher.weapons)
            {
                if (w == null) continue;
                if (w.TryGetComponent(out G1Pistol p)) { data.clips[0] = p.clip; data.reserves[0] = p.reserve; }
                else if (w.TryGetComponent(out G1Smg s)) { data.clips[1] = s.clip; data.reserves[1] = s.reserve; }
                else if (w.TryGetComponent(out G1Shotgun sh)) { data.clips[2] = sh.clip; data.reserves[2] = sh.reserve; }
                else if (w.TryGetComponent(out G1Magnum m)) { data.clips[3] = m.clip; data.reserves[3] = m.reserve; }
                else if (w.TryGetComponent(out G1Grenade g)) { data.grenades = g.count; }
            }
        }
        Save(data);
    }

    public static void SaveLevelClear(string nextSceneName, GameObject player)
    {
        var data = Load();
        data.currentScene = nextSceneName;
        data.scene = nextSceneName;

        if (nextSceneName == "Level2" && data.maxUnlockedLevelIndex < 2) data.maxUnlockedLevelIndex = 2;
        if (nextSceneName == "Level3" && data.maxUnlockedLevelIndex < 3) data.maxUnlockedLevelIndex = 3;

        if (player != null)
        {
            var health = player.GetComponent<HealthSystem>();
            if (health != null)
            {
                data.health = Mathf.Max(50f, health.CurrentHealth);
                data.armor = health.Armor;
            }

            var switcher = player.GetComponentInChildren<WeaponSwitcher>(true);
            if (switcher != null && switcher.unlocked != null)
            {
                data.unlockMask = 0;
                for (int i = 0; i < switcher.unlocked.Length; i++)
                    if (switcher.unlocked[i]) data.unlockMask |= (1 << i);

                foreach (var w in switcher.weapons)
                {
                    if (w == null) continue;
                    if (w.TryGetComponent(out G1Pistol p)) { data.clips[0] = p.clip; data.reserves[0] = p.reserve; }
                    else if (w.TryGetComponent(out G1Smg s)) { data.clips[1] = s.clip; data.reserves[1] = s.reserve; }
                    else if (w.TryGetComponent(out G1Shotgun sh)) { data.clips[2] = sh.clip; data.reserves[2] = sh.reserve; }
                    else if (w.TryGetComponent(out G1Magnum m)) { data.clips[3] = m.clip; data.reserves[3] = m.reserve; }
                    else if (w.TryGetComponent(out G1Grenade g)) { data.grenades = g.count; }
                }
            }
        }

        data.isLevelClearTransition = true;
        Save(data);
    }

    public static void ClearSave()
    {
        if (File.Exists(Path))
        {
            try { File.Delete(Path); } catch {}
        }
        PlayerPrefs.DeleteKey(ContinueFlag);
        PlayerPrefs.Save();
    }

    public static void Continue()
    {
        if (!HasSave) return;
        var d = Load();
        PlayerPrefs.SetInt(ContinueFlag, 1);
        PlayerPrefs.Save();
        SceneManager.LoadScene(string.IsNullOrEmpty(d.currentScene) ? "TestScene" : d.currentScene);
    }

    public static bool ConsumeContinuePending()
    {
        if (PlayerPrefs.GetInt(ContinueFlag, 0) != 1)
            return false;
        PlayerPrefs.SetInt(ContinueFlag, 0);
        return true;
    }
}

/// On the player: applies the on-disk save after scene loads if continue or level clear is pending.
public sealed class G1SaveApplier : MonoBehaviour
{
    void Start()
    {
        if (!G1SaveSystem.HasSave)
            return;
        bool isContinue = G1SaveSystem.ConsumeContinuePending();
        var d = G1SaveSystem.Load();
        if (!isContinue && !d.isLevelClearTransition)
            return;

        if (isContinue && (d.x != 0 || d.y != 0 || d.z != 0))
        {
            var cc = GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            transform.position = new Vector3(d.x, d.y, d.z);
            transform.rotation = Quaternion.Euler(0f, d.yaw, 0f);
            if (cc) cc.enabled = true;
        }

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
                if (w == null) continue;
                if (w.TryGetComponent(out G1Pistol p)) { if (d.clips[0] > 0) p.clip = d.clips[0]; if (d.reserves[0] > 0) p.reserve = d.reserves[0]; }
                else if (w.TryGetComponent(out G1Smg s)) { if (d.clips[1] > 0) s.clip = d.clips[1]; if (d.reserves[1] > 0) s.reserve = d.reserves[1]; }
                else if (w.TryGetComponent(out G1Shotgun sh)) { if (d.clips[2] > 0) sh.clip = d.clips[2]; if (d.reserves[2] > 0) sh.reserve = d.reserves[2]; }
                else if (w.TryGetComponent(out G1Magnum m)) { if (d.clips[3] > 0) m.clip = d.clips[3]; if (d.reserves[3] > 0) m.reserve = d.reserves[3]; }
                else if (w.TryGetComponent(out G1Grenade g)) { if (d.grenades > 0) g.count = d.grenades; }
            }
        }
        Debug.Log("Save data applied successfully.");
    }
}
