using UnityEngine;

/// HEV battery pickup: grants armor points on touch. Static Create() builds a
/// spinning teal cell; the wall-mounted charger variant is G1WallCharger.
public class G1ArmorPack : MonoBehaviour
{
    public float armorAmount = 25f;

    void Update()
    {
        transform.Rotate(Vector3.up * 45f * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;
        var health = other.GetComponent<HealthSystem>();
        if (health != null && health.Armor < health.maxArmor)
        {
            health.AddArmor(armorAmount);
            G1Audio.Play2D("pickup", 0.7f, 1.1f);
            Destroy(gameObject);
        }
    }

    public static GameObject Create(Vector3 position, float amount = 25f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "ArmorPack";
        go.transform.position = position;
        go.transform.localScale = new Vector3(0.3f, 0.34f, 0.18f);
        go.GetComponent<BoxCollider>().isTrigger = true;

        var mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.1f, 0.6f, 0.7f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.05f, 0.4f, 0.5f));
        go.GetComponent<Renderer>().sharedMaterial = mat;

        go.AddComponent<G1ArmorPack>().armorAmount = amount;
        return go;
    }
}

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
        if (charge <= 0f || health.Armor >= health.maxArmor)
        {
            G1Audio.Play("hit_thunk", transform.position, 0.4f, 0.6f);   // empty click
            return;
        }
        float give = Mathf.Min(ratePerUse, charge, health.maxArmor - health.Armor);
        health.AddArmor(give);
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
