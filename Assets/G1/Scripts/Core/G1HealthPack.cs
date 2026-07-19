using UnityEngine;

/// Procedural medical kit pickup. Restores 25 health to player on touch.
public class G1HealthPack : MonoBehaviour
{
    public float healAmount = 25f;

    void Update()
    {
        // Slowly spin for classic retro pickup look
        transform.Rotate(Vector3.up * 45f * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            var health = other.GetComponent<HealthSystem>();
            if (health != null && health.CurrentHealth < health.maxHealth)
            {
                health.Heal(healAmount);
                
                // Play simple visual cue (flash screen green or log)
                Debug.Log("Healed 25 HP!");
                Destroy(gameObject);
            }
        }
    }

    public static GameObject Create(Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "HealthPack";
        go.transform.position = position;
        go.transform.localScale = new Vector3(0.35f, 0.2f, 0.25f);
        
        var col = go.GetComponent<BoxCollider>();
        col.isTrigger = true;

        // Fleshy red kit body
        var r = go.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.85f, 0.15f, 0.15f);
        mat.SetFloat("_Glossiness", 0.0f);
        r.sharedMaterial = mat;

        // Visual Cross (White lines)
        var crossH = GameObject.CreatePrimitive(PrimitiveType.Cube);
        DestroyImmediate(crossH.GetComponent<Collider>());
        crossH.transform.SetParent(go.transform, false);
        crossH.transform.localPosition = new Vector3(0f, 0.51f, 0f);
        crossH.transform.localScale = new Vector3(0.6f, 0.1f, 0.2f);
        
        var wMat = new Material(Shader.Find("Standard"));
        wMat.color = Color.white;
        wMat.SetFloat("_Glossiness", 0.0f);
        crossH.GetComponent<Renderer>().sharedMaterial = wMat;

        var crossV = GameObject.CreatePrimitive(PrimitiveType.Cube);
        DestroyImmediate(crossV.GetComponent<Collider>());
        crossV.transform.SetParent(go.transform, false);
        crossV.transform.localPosition = new Vector3(0f, 0.51f, 0f);
        crossV.transform.localScale = new Vector3(0.2f, 0.1f, 0.6f);
        crossV.GetComponent<Renderer>().sharedMaterial = wMat;

        go.AddComponent<G1HealthPack>();
        return go;
    }
}
