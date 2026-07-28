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

// -------------------------------------------------------------- fabricator
/// Ammunition dispenser. Charges up, hands out a magazine, cools down.
///
/// A crate of ammo on the floor is a pickup; this is a machine, and the
/// difference matters in an armoury the player is meant to come back to.
public sealed class G1Fabricator : MonoBehaviour, IUsable
{
    public Renderer statusRenderer;
    public float cycleTime = 6f;
    public int roundsPerCycle = 30;

    float ready = 1f;

    static readonly Color Charged = new Color(0.15f, 0.85f, 1f);
    static readonly Color Charging = new Color(0.9f, 0.4f, 0.05f);

    void Update()
    {
        if (ready < 1f)
        {
            ready = Mathf.Clamp01(ready + Time.deltaTime / Mathf.Max(0.1f, cycleTime));
            Tint();
        }
    }

    void Tint()
    {
        if (statusRenderer == null) return;
        var c = Color.Lerp(Charging, Charged, ready);
        var m = statusRenderer.material;
        m.color = c;
        if (m.HasProperty("_EmissionColor"))
        {
            m.SetColor("_EmissionColor", c * (0.4f + ready));
            m.EnableKeyword("_EMISSION");
        }
    }

    public void OnUse(GameObject user)
    {
        if (ready < 1f)
        {
            G1Audio.Play("hit_thunk", transform.position, 0.4f, 0.6f);
            Debug.Log($"Fabricator charging — {Mathf.RoundToInt(ready * 100f)}%");
            return;
        }
        var switcher = user != null ? user.GetComponentInChildren<WeaponSwitcher>(true) : null;
        if (switcher == null) return;

        // Every weapon type carries its own reserve cap; there is no shared
        // field on WeaponBase to add to, so this fills the same four the ammo
        // pickup does, to the same ceilings.
        int given = 0;
        foreach (var go in switcher.weapons)
        {
            if (go == null) continue;
            var pistol = go.GetComponent<G1Pistol>();
            if (pistol != null) { int b = pistol.reserve; pistol.reserve = Mathf.Min(b + roundsPerCycle, 68); given += pistol.reserve - b; }
            var smg = go.GetComponent<G1Smg>();
            if (smg != null) { int b = smg.reserve; smg.reserve = Mathf.Min(b + roundsPerCycle, 150); given += smg.reserve - b; }
            var shotgun = go.GetComponent<G1Shotgun>();
            if (shotgun != null) { int b = shotgun.reserve; shotgun.reserve = Mathf.Min(b + roundsPerCycle / 3, 24); given += shotgun.reserve - b; }
            var magnum = go.GetComponent<G1Magnum>();
            if (magnum != null) { int b = magnum.reserve; magnum.reserve = Mathf.Min(b + roundsPerCycle / 5, 18); given += magnum.reserve - b; }
        }
        ready = 0f;
        Tint();
        G1Audio.Play("door_servo", transform.position, 1.4f, 0.4f);
        Debug.Log($"Fabricator dispensed {given} rounds");
    }
}
