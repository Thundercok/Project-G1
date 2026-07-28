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

