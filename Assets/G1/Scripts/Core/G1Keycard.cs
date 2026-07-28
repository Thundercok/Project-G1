using UnityEngine;

// Split out of G1AccessControl.cs so the class name matches the file name.
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

/// A card reader wired to a group of doors.
///
/// Every door, shutter and barrier the generator tagged with the same group
/// name is opened together, which is what lets one reader at the airlock
/// release both leaves of it, and one switch release every sealed door on the
/// site once main power is back.
public sealed class G1Keycard : MonoBehaviour, IUsable
{
    public string group = "";
    public MonoBehaviour[] targets = new MonoBehaviour[0];
    public bool powered = true;
    public string objectiveId = "";       // optional: credit for getting in

    public Renderer led;
    bool used;

    static readonly Color Locked = new Color(0.85f, 0.12f, 0.08f);
    static readonly Color Open = new Color(0.15f, 0.9f, 0.35f);

    public void OnUse(GameObject user)
    {
        if (!powered)
        {
            G1Audio.Play("hit_thunk", transform.position, 0.5f, 0.7f);
            Debug.Log($"Reader [{group}]: NO POWER");
            return;
        }

        foreach (var t in targets)
        {
            switch (t)
            {
                case G1BlastDoor bd: bd.Unlock(); bd.Open(); break;
                case G1RollupDoor rd: rd.Unlock(); rd.Open(); break;
                case G1BoomBarrier bb: bb.Unlock(); bb.Open(); break;
            }
        }
        Relamp();

        if (!used && !string.IsNullOrEmpty(objectiveId))
        {
            used = true;
            G1ObjectiveManager.Instance?.IncrementProgress(objectiveId, 1);
        }
    }

    void Relamp()
    {
        if (led == null) return;
        var m = led.material;
        m.color = Open;
        if (m.HasProperty("_EmissionColor"))
        {
            m.SetColor("_EmissionColor", Open * 1.6f);
            m.EnableKeyword("_EMISSION");
        }
    }

    void Start() { if (led != null && !powered) led.material.color = Locked; }
}
