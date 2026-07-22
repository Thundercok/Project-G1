using UnityEngine;

/// Simple, reliable campaign save & persistence system.
/// Stores current level, max unlocked level, health, weapons, ammo, and grenades in PlayerPrefs.
public static class G1SaveSystem
{
    const string SaveKey = "G1_Campaign_SaveData";

    [System.Serializable]
    public class SaveData
    {
        public string currentScene = "TestScene";
        public int maxUnlockedLevelIndex = 1; // 1: Level 1 (TestScene), 2: Level 2, 3: Level 3
        public float health = 100f;
        public int unlockMask = 1; // Bit 0: Pistol
        public int grenades = 0;
        public int[] clips = new int[4];
        public int[] reserves = new int[4];
        public bool isLevelClearTransition = false;

        public SaveData()
        {
            clips = new int[4];
            reserves = new int[4];
        }
    }

    public static bool HasSaveData()
    {
        return PlayerPrefs.HasKey(SaveKey);
    }

    public static SaveData Load()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
            return new SaveData();

        string json = PlayerPrefs.GetString(SaveKey);
        try
        {
            var data = JsonUtility.FromJson<SaveData>(json);
            if (data.clips == null || data.clips.Length < 4) data.clips = new int[4];
            if (data.reserves == null || data.reserves.Length < 4) data.reserves = new int[4];
            return data ?? new SaveData();
        }
        catch
        {
            return new SaveData();
        }
    }

    public static void Save(SaveData data)
    {
        if (data == null) return;
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
        Debug.Log($"[G1SaveSystem] Campaign saved. Level: {data.currentScene}, Unlocked: {data.maxUnlockedLevelIndex}");
    }

    public static void ClearSave()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
    }

    /// Called by G1LevelExitTrigger when player clears a level.
    public static void SaveLevelClear(string nextSceneName, GameObject player)
    {
        var data = Load();
        data.currentScene = nextSceneName;

        // Update level unlocks index
        if (nextSceneName == "Level2" && data.maxUnlockedLevelIndex < 2) data.maxUnlockedLevelIndex = 2;
        if (nextSceneName == "Level3" && data.maxUnlockedLevelIndex < 3) data.maxUnlockedLevelIndex = 3;

        // Capture player stats
        if (player != null)
        {
            var health = player.GetComponent<HealthSystem>();
            if (health != null)
                data.health = Mathf.Max(50f, health.CurrentHealth); // Minimum 50 HP on new level start for fair play

            var switcher = player.GetComponentInChildren<WeaponSwitcher>(true);
            if (switcher != null && switcher.unlocked != null)
            {
                data.unlockMask = 0;
                for (int i = 0; i < switcher.unlocked.Length; i++)
                {
                    if (switcher.unlocked[i])
                        data.unlockMask |= (1 << i);
                }

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
}
