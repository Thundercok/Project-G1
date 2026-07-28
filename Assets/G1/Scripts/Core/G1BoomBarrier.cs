using UnityEngine;

// Split out of G1BaseEquipment.cs so the class name matches the file name.
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

// ---------------------------------------------------------------- barriers
/// The boom arm at a vehicle checkpoint. Rotates about its post.
public sealed class G1BoomBarrier : MonoBehaviour, IUsable
{
    public Transform arm;
    public float openAngle = -80f;      // negative pitches the arm upward
    public float moveTime = 1.4f;
    public bool locked;
    public string label = "BARRIER";

    public bool IsOpen { get; private set; }

    Quaternion closedRot;
    float t;
    int dir;
    bool moving;

    void Awake()
    {
        if (arm == null) arm = transform;
        closedRot = arm.localRotation;
    }

    void Update()
    {
        if (!moving) return;
        t = Mathf.Clamp01(t + dir * Time.deltaTime / Mathf.Max(0.05f, moveTime));
        arm.localRotation = closedRot * Quaternion.Euler(openAngle * t, 0f, 0f);
        if (t <= 0f || t >= 1f) { moving = false; IsOpen = t >= 1f; }
    }

    public void Open() { if (!locked) { dir = 1; moving = true; } }
    public void Close() { dir = -1; moving = true; }
    public void Unlock() { locked = false; }

    public void OnUse(GameObject user)
    {
        if (locked) { G1Audio.Play("hit_thunk", transform.position, 0.5f, 0.7f); return; }
        G1Audio.Play("door_servo", transform.position, 0.7f, 0.45f);
        if (IsOpen) Close(); else Open();
    }
}
