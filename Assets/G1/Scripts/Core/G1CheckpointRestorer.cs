using UnityEngine;

// Split out of G1Checkpoint.cs so the class name matches the file name.
//
// Unity only creates a MonoScript for the type whose name matches its file. Any
// other MonoBehaviour in that file can still be added by AddComponent while the
// editor session lasts, and then serialises into the scene as `m_Script:
// {fileID: 0}` — a component that silently is not there the next time the
// scene is opened. Twenty-eight of them had accumulated, including every quest
// zone, every objective-on-death and the extraction gate, which is why walking
// into a quest trigger did nothing and why killing the boss never ticked the
// objective. It fails without an error at any point, so the only defence is the
// naming rule.

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
