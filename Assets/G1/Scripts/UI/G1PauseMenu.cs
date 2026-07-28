using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runtime-attached gameplay pause menu. It keeps the menu out of the generated
/// scenes while making Escape consistently pause every playable level.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class G1PauseMenu : MonoBehaviour
{
    public bool IsOpen => isOpen;

    bool isOpen;
    bool mouseLookWasEnabled;
    bool movementWasEnabled;
    bool audioWasPaused;
    MouseLook mouseLook;
    PlayerMovement movement;
    G1WeaponWheel weaponWheel;
    G1SettingsPanel settingsPanel;
    Font font;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        // Attach now for the first scene
        AttachToPlayer();
        // Re-attach after every scene load (Level 2, Level 3, HugeMap, etc.)
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AttachToPlayer();
    }

    static void AttachToPlayer()
    {
        // Skip menu scene — it has its own UI
        if (SceneManager.GetActiveScene().name == "MenuScene")
            return;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && player.GetComponent<G1PauseMenu>() == null)
            player.AddComponent<G1PauseMenu>();
    }

    void Awake()
    {
        mouseLook = GetComponentInChildren<MouseLook>(true);
        movement = GetComponent<PlayerMovement>();
        weaponWheel = GetComponent<G1WeaponWheel>();
        settingsPanel = GetComponent<G1SettingsPanel>();
        font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
    }

    void Update()
    {
        if (IsCutsceneActive())
            return;

        if (settingsPanel != null && settingsPanel.visible)
            return;

        // The weapon wheel owns TAB and its own time dilation. Let it close before
        // Escape can open the regular pause interface.
        if (weaponWheel != null && weaponWheel.isOpen)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isOpen)
                Resume();
            else
                Pause();
        }
    }

    void Pause()
    {
        isOpen = true;
        mouseLookWasEnabled = mouseLook != null && mouseLook.enabled;
        movementWasEnabled = movement != null && movement.enabled;
        audioWasPaused = AudioListener.pause;

        if (mouseLook != null) mouseLook.enabled = false;
        if (movement != null) movement.enabled = false;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Resume()
    {
        if (!isOpen)
            return;

        isOpen = false;
        if (mouseLook != null) mouseLook.enabled = mouseLookWasEnabled;
        if (movement != null) movement.enabled = movementWasEnabled;
        Time.timeScale = 1f;
        AudioListener.pause = audioWasPaused;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OpenSettings()
    {
        if (settingsPanel == null)
            settingsPanel = gameObject.AddComponent<G1SettingsPanel>();
        settingsPanel.visible = true;
    }

    void RestartLevel()
    {
        Resume();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void ReturnToMainMenu()
    {
        Resume();
        SceneManager.LoadScene("MenuScene");
    }

    void OnDisable()
    {
        Resume();
    }

    void OnGUI()
    {
        if (!isOpen || (settingsPanel != null && settingsPanel.visible))
            return;

        var overlay = new Color(0f, 0f, 0f, 0.72f);
        Color previousColor = GUI.color;
        GUI.color = overlay;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previousColor;

        const float panelW = 420f;
        const float panelH = 320f;
        float panelX = Screen.width * 0.5f - panelW * 0.5f;
        float panelY = Screen.height * 0.5f - panelH * 0.5f;
        DrawPanel(new Rect(panelX, panelY, panelW, panelH));

        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            font = font
        };
        titleStyle.normal.textColor = new Color(0.16f, 0.75f, 0.75f);
        GUI.Label(new Rect(panelX, panelY + 24f, panelW, 40f), "SYSTEM PAUSED", titleStyle);

        var buttonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            font = font,
            normal = { textColor = new Color(0.86f, 0.93f, 0.95f) },
            hover = { textColor = new Color(1f, 0.82f, 0.25f) },
            active = { textColor = Color.white }
        };

        float buttonX = panelX + 48f;
        float buttonW = panelW - 96f;
        if (GUI.Button(new Rect(buttonX, panelY + 88f, buttonW, 38f), "[ RESUME ]", buttonStyle))
            Resume();
        if (GUI.Button(new Rect(buttonX, panelY + 136f, buttonW, 38f), "[ SETTINGS ]", buttonStyle))
            OpenSettings();
        if (GUI.Button(new Rect(buttonX, panelY + 184f, buttonW, 38f), "[ RESTART LEVEL ]", buttonStyle))
            RestartLevel();
        if (GUI.Button(new Rect(buttonX, panelY + 232f, buttonW, 38f), "[ MAIN MENU ]", buttonStyle))
            ReturnToMainMenu();

        var hintStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            font = font
        };
        hintStyle.normal.textColor = new Color(0.56f, 0.66f, 0.7f);
        GUI.Label(new Rect(panelX, panelY + 282f, panelW, 20f), "ESC  •  RESUME", hintStyle);
    }

    void DrawPanel(Rect rect)
    {
        Color previousColor = GUI.color;
        var tex = Texture2D.whiteTexture;
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), tex);
        GUI.color = new Color(0.025f, 0.05f, 0.07f, 0.96f);
        GUI.DrawTexture(rect, tex);
        GUI.color = new Color(0.16f, 0.75f, 0.75f, 0.85f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, 4f, rect.height), tex);
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f), tex);
        GUI.color = previousColor;
    }

    static bool IsCutsceneActive()
    {
        return G1IntroStory.IsActive
            || G1EndingCutscene.IsPlaying
            || G1TrailerPlayer.IsPlaying
            || (G1CutsceneManager.Instance != null && G1CutsceneManager.Instance.isCutsceneActive);
    }
}
