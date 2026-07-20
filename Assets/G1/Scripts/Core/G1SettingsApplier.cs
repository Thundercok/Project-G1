using UnityEngine;

/// Applies saved settings on level load (Awake runs before CameraEffects
/// caches baseFOV in Start, so the FOV sticks).
public sealed class G1SettingsApplier : MonoBehaviour
{
    void Awake()
    {
        AudioListener.volume = PlayerPrefs.GetFloat("G1_MasterVolume", 0.8f);
        var cam = GetComponentInChildren<Camera>();
        if (cam)
            cam.fieldOfView = PlayerPrefs.GetFloat("G1_FOV", 75f);
    }
}
