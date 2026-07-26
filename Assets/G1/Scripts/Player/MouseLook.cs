using UnityEngine;

/// Sits on the camera. Yaw turns the player body, pitch tilts the camera.
public class MouseLook : MonoBehaviour
{
    public Transform body;
    public float sensitivity = 2.2f;
    public float pitchLimit = 89f;

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
        // Gameplay pause owns Escape whenever it is present on the player.
        // Keep the legacy cursor-release behavior for scenes without a player.
        var pauseMenu = GetComponentInParent<G1PauseMenu>();
        if (pauseMenu != null && pauseMenu.IsOpen)
            return;

        if (pauseMenu == null && Input.GetKeyDown(KeyCode.Escape))
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

        float mx = Input.GetAxisRaw("Mouse X") * sensitivity;
        float my = Input.GetAxisRaw("Mouse Y") * sensitivity;
        body.Rotate(0f, mx, 0f);
        pitch = Mathf.Clamp(pitch - my, -pitchLimit, pitchLimit);
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
