using UnityEngine;

/// Procedural ammo box pickup. Replenishes reserve ammo for all carried weapons.
public class G1AmmoPack : MonoBehaviour
{
    void Update()
    {
        // Slowly spin for classic retro pickup look
        transform.Rotate(Vector3.up * 45f * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            var switcher = other.GetComponentInChildren<WeaponSwitcher>();
            if (switcher != null)
            {
                bool loadedAny = false;
                foreach (var wGo in switcher.weapons)
                {
                    var weapon = wGo.GetComponent<WeaponBase>();
                    if (weapon != null)
                    {
                        if (weapon is G1Pistol pistol && pistol.reserve < 68)
                        {
                            pistol.reserve = Mathf.Min(pistol.reserve + 17, 68);
                            loadedAny = true;
                        }
                        else if (weapon is G1Smg smg && smg.reserve < 150)
                        {
                            smg.reserve = Mathf.Min(smg.reserve + 50, 150);
                            loadedAny = true;
                        }
                        else if (weapon is G1Shotgun shotgun && shotgun.reserve < 24)
                        {
                            shotgun.reserve = Mathf.Min(shotgun.reserve + 8, 24);
                            loadedAny = true;
                        }
                        else if (weapon is G1Magnum magnum && magnum.reserve < 18)
                        {
                            magnum.reserve = Mathf.Min(magnum.reserve + 6, 18);
                            loadedAny = true;
                        }
                    }
                }

                if (loadedAny)
                {
                    Debug.Log("Ammo Refilled!");
                    Destroy(gameObject);
                }
            }
        }
    }

    public static GameObject Create(Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "AmmoPack";
        go.transform.position = position;
        go.transform.localScale = new Vector3(0.4f, 0.25f, 0.2f);

        var col = go.GetComponent<BoxCollider>();
        col.isTrigger = true;

        // Olive military box color
        var r = go.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.25f, 0.35f, 0.2f);
        mat.SetFloat("_Glossiness", 0.0f);
        r.sharedMaterial = mat;

        // Brass stripe details
        var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        DestroyImmediate(stripe.GetComponent<Collider>());
        stripe.transform.SetParent(go.transform, false);
        stripe.transform.localPosition = new Vector3(0f, 0f, 0f);
        stripe.transform.localScale = new Vector3(0.2f, 1.02f, 1.02f);
        
        var bMat = new Material(Shader.Find("Standard"));
        bMat.color = new Color(0.75f, 0.6f, 0.2f); // brass yellow
        bMat.SetFloat("_Glossiness", 0.3f);
        stripe.GetComponent<Renderer>().sharedMaterial = bMat;

        go.AddComponent<G1AmmoPack>();
        return go;
    }
}
