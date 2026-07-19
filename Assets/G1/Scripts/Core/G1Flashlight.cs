using UnityEngine;

public class G1Flashlight : MonoBehaviour
{
    public float battery = 100f;
    public float drainRate = 8f;     // % per second
    public float chargeRate = 5f;    // % per second
    
    private Light spotLight;
    private bool isActive = false;

    public float Battery => battery;
    public bool IsActive => isActive;

    void Start()
    {
        // Create the flashlight spotlight child
        GameObject lightGo = new GameObject("Flashlight_Spot");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = Vector3.zero;
        lightGo.transform.localRotation = Quaternion.identity;

        spotLight = lightGo.AddComponent<Light>();
        spotLight.type = LightType.Spot;
        spotLight.range = 25f;
        spotLight.spotAngle = 40f;
        spotLight.intensity = 2.0f;
        spotLight.color = new Color(1f, 0.97f, 0.9f);
        spotLight.shadows = LightShadows.Soft;
        spotLight.enabled = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            isActive = !isActive;
            spotLight.enabled = isActive && battery > 0f;
            // Play click sound
            G1Audio.Play2D("door_servo", 0.4f, isActive ? 1.5f : 1.2f);
        }

        if (isActive)
        {
            battery = Mathf.Max(battery - drainRate * Time.deltaTime, 0f);
            if (battery <= 0f)
            {
                spotLight.enabled = false;
            }
        }
        else
        {
            battery = Mathf.Min(battery + chargeRate * Time.deltaTime, 100f);
        }

        if (spotLight.enabled != (isActive && battery > 0f))
        {
            spotLight.enabled = isActive && battery > 0f;
        }
    }
}
