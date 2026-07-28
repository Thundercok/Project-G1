using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Split out of G1SaveSystem.cs so the class name matches the file name.
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
