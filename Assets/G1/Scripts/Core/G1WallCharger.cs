using UnityEngine;

// Split out of G1ArmorPack.cs so the class name matches the file name.
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

/// Wall-mounted HEV charger: press E to drain its reserve into your armor.
public sealed class G1WallCharger : MonoBehaviour, IUsable
{
    public float charge = 75f;         // remaining AP the unit can dispense
    public float ratePerUse = 15f;

    Renderer statusLight;

    void Start()
    {
        // small status lamp so depletion is readable
        var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(lamp.GetComponent<Collider>());
        lamp.name = "ChargerLamp";
        lamp.transform.SetParent(transform, false);
        lamp.transform.localPosition = new Vector3(0f, 0.35f, -0.55f);
        lamp.transform.localScale = new Vector3(0.3f, 0.12f, 0.1f);
        statusLight = lamp.GetComponent<Renderer>();
        statusLight.sharedMaterial = Emissive(new Color(0.1f, 0.9f, 0.4f));
    }

    public void OnUse(GameObject user)
    {
        var health = user.GetComponent<HealthSystem>();
        if (health == null)
            return;

        // A charger tops up the whole suit, not just the plating — with sprint
        // drawing on the aux cell, a station next to a long run is worth
        // stopping at even when your armor is already full.
        var suit = user.GetComponent<G1SuitPower>();
        bool wantsArmor = health.Armor < health.maxArmor;
        bool wantsAux = suit != null && suit.Power < suit.maxPower;

        if (charge <= 0f || (!wantsArmor && !wantsAux))
        {
            G1Audio.Play("hit_thunk", transform.position, 0.4f, 0.6f);   // empty click
            return;
        }

        float give = Mathf.Min(ratePerUse, charge);
        if (wantsArmor)
        {
            give = Mathf.Min(give, health.maxArmor - health.Armor);
            health.AddArmor(give);
        }
        else
        {
            suit.Recharge(give);
        }
        charge -= give;
        G1Audio.Play("door_servo", transform.position, 0.6f, 1.5f);
        if (statusLight && charge <= 0f)
            statusLight.sharedMaterial = Emissive(new Color(0.7f, 0.1f, 0.1f));
    }

    static Material Emissive(Color c)
    {
        var m = new Material(Shader.Find("Standard"));
        m.color = c;
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c);
        return m;
    }

    /// Build a wall charger box at a position (call from scene builders).
    public static GameObject Create(Vector3 pos, float charge = 75f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "HEVCharger";
        go.transform.position = pos;
        go.transform.localScale = new Vector3(0.8f, 0.9f, 0.3f);
        var mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.85f, 0.65f, 0.1f);       // hazard yellow housing
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.AddComponent<G1WallCharger>().charge = charge;
        return go;
    }
}
