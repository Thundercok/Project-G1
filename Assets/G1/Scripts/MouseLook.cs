using UnityEngine;

/// Sits on the camera. Yaw turns the player body, pitch tilts the camera.
public class MouseLook : MonoBehaviour
{
    public Transform body;
    public float sensitivity = 2.2f;
    public float pitchLimit = 89f;
    /// Runtime multiplier, not a setting — reset to 1 when nothing is scaling it.
    [HideInInspector] public float sensitivityScale = 1f;

    float pitch;
    public float Pitch => pitch;

    void Start()
    {
        sensitivity = PlayerPrefs.GetFloat("G1_Sensitivity", sensitivity);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked
            && !G1MobSpawnerToolbox.IsOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        // The active weapon pulls this down while aiming down sights: a zoomed
        // view at hip sensitivity is unusable, because the same mouse travel
        // now sweeps a much narrower slice of the world.
        float mx = Input.GetAxisRaw("Mouse X") * sensitivity * sensitivityScale;
        float my = Input.GetAxisRaw("Mouse Y") * sensitivity * sensitivityScale;
        body.Rotate(0f, mx, 0f);
        pitch = Mathf.Clamp(pitch - my, -pitchLimit, pitchLimit);
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
