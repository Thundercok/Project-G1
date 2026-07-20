using UnityEngine;

/// Checkpoint trigger: saves player position, health, weapon unlocks, and
/// ammo to PlayerPrefs. On death, G1PlayerDeath sets a restore flag and
/// reloads the scene; G1CheckpointRestorer (on the player) applies the save.
/// Visual: spinning teal pyramid that dims once activated.
public sealed class G1Checkpoint : MonoBehaviour
{
    [System.Serializable]
    public struct Data
    {
        public float x, y, z, yaw, health;
        public int unlockMask, grenades;
        public int[] clips;
        public int[] reserves;
    }

    const string Key = "G1_CP_Data";
    const string Flag = "G1_CP_RestorePending";

    bool activated;
    Transform visual;
    Material visMat;

    void Awake()
    {
        var col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.2f;
        BuildVisual();
    }

    void Update()
    {
        if (visual)
            visual.Rotate(0f, 45f * Time.deltaTime, 0f, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        if (activated || !other.CompareTag("Player"))
            return;
        activated = true;
        Save(other.gameObject);
        G1Audio.Play2D("pickup", 0.7f, 1.3f);
        if (visMat)
            visMat.SetColor("_EmissionColor",
                            new Color(0.16f, 0.75f, 0.75f) * 0.3f);
    }

    static void Save(GameObject player)
    {
        var d = new Data
        {
            x = player.transform.position.x,
            y = player.transform.position.y,
            z = player.transform.position.z,
            yaw = player.transform.eulerAngles.y,
            health = player.GetComponent<HealthSystem>()?.CurrentHealth ?? 100f,
            clips = new int[4],
            reserves = new int[4],
        };
        var switcher = player.GetComponentInChildren<WeaponSwitcher>(true);
        if (switcher != null && switcher.unlocked != null)
        {
            for (int i = 0; i < switcher.unlocked.Length; i++)
                if (switcher.unlocked[i])
                    d.unlockMask |= 1 << i;
            foreach (var w in switcher.weapons)
            {
                if (w.TryGetComponent(out G1Pistol p)) { d.clips[0] = p.clip; d.reserves[0] = p.reserve; }
                else if (w.TryGetComponent(out G1Smg s)) { d.clips[1] = s.clip; d.reserves[1] = s.reserve; }
                else if (w.TryGetComponent(out G1Shotgun sh)) { d.clips[2] = sh.clip; d.reserves[2] = sh.reserve; }
                else if (w.TryGetComponent(out G1Magnum m)) { d.clips[3] = m.clip; d.reserves[3] = m.reserve; }
                else if (w.TryGetComponent(out G1Grenade g)) { d.grenades = g.count; }
            }
        }
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(d));
        PlayerPrefs.Save();
        Debug.Log("Checkpoint saved");
    }

    public static bool HasSave => PlayerPrefs.HasKey(Key);
    public static void MarkRestorePending() => PlayerPrefs.SetInt(Flag, 1);
    public static bool ConsumeRestorePending()
    {
        if (PlayerPrefs.GetInt(Flag, 0) != 1)
            return false;
        PlayerPrefs.SetInt(Flag, 0);
        return true;
    }

    public static Data Load()
        => JsonUtility.FromJson<Data>(PlayerPrefs.GetString(Key));

    void BuildVisual()
    {
        var go = new GameObject("CheckpointVisual");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 0.35f;
        visual = go.transform;
        var mesh = new Mesh();
        float s = 0.15f, h = 0.3f;
        mesh.vertices = new[]
        {
            new Vector3(-s, 0, -s), new Vector3(s, 0, -s),
            new Vector3(s, 0, s), new Vector3(-s, 0, s),
            new Vector3(0, h, 0),
        };
        mesh.triangles = new[] { 0, 4, 1, 1, 4, 2, 2, 4, 3, 3, 4, 0, 2, 1, 0, 3, 2, 0 };
        mesh.RecalculateNormals();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        visMat = new Material(Shader.Find("Standard"));
        var teal = new Color(0.16f, 0.75f, 0.75f);
        visMat.color = teal;
        visMat.EnableKeyword("_EMISSION");
        visMat.SetColor("_EmissionColor", teal * 1.2f);
        go.AddComponent<MeshRenderer>().sharedMaterial = visMat;
    }
}

/// Lives on the player: applies the checkpoint save after a death reload.
public sealed class G1CheckpointRestorer : MonoBehaviour
{
    void Start()
    {
        if (!G1Checkpoint.ConsumeRestorePending() || !G1Checkpoint.HasSave)
            return;
        var d = G1Checkpoint.Load();

        var cc = GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        transform.position = new Vector3(d.x, d.y, d.z);
        transform.rotation = Quaternion.Euler(0f, d.yaw, 0f);
        if (cc) cc.enabled = true;

        var health = GetComponent<HealthSystem>();
        if (health)
            health.Heal(Mathf.Max(25f, d.health));    // never respawn near-dead

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
        Debug.Log("Checkpoint restored");
    }
}
