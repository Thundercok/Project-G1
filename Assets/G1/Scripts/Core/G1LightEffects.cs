using UnityEngine;

public class G1LightEffects : MonoBehaviour
{
    public enum EffectType { Flicker, Pulse }
    public EffectType effectType = EffectType.Flicker;

    private Light lt;
    private float baseIntensity;

    void Start()
    {
        lt = GetComponent<Light>();
        if (lt != null) baseIntensity = lt.intensity;
    }

    void Update()
    {
        if (lt == null) return;

        if (effectType == EffectType.Flicker)
        {
            // Damaged fluorescent light flicker
            if (Random.value > 0.88f)
            {
                lt.intensity = Random.Range(0.15f, baseIntensity * 1.15f);
            }
            else
            {
                lt.intensity = Mathf.Lerp(lt.intensity, baseIntensity, 10f * Time.deltaTime);
            }
        }
        else if (effectType == EffectType.Pulse)
        {
            // Smooth portal sine pulse
            lt.intensity = baseIntensity * (0.6f + Mathf.Sin(Time.time * 3.5f) * 0.4f);
        }
    }
}
