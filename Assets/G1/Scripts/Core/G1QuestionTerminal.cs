using UnityEngine;

/// A console that poses a story question (press E to use). Answer correctly and
/// the level exit unlocks, an optional objective completes, and an optional door
/// opens — this is the "solve a puzzle to pass the level" gate. Wrong answers can
/// be retried. While the panel is open the player is paused and the cursor freed
/// so the answer buttons don't fight the movement/weapon keys.
public class G1QuestionTerminal : MonoBehaviour, IUsable
{
    [TextArea] public string question = "What is the Threshold truly a door to?";
    public string[] options = { "Another planet", "Another point in TIME", "A parallel dimension" };
    public int correctIndex = 1;
    public string objectiveId = "";        // optional objective to complete on solve
    public SlidingDoor targetDoor;         // optional door to open on solve
    public bool lockExitUntilSolved = true;

    bool solved, panelOpen, wrongFlash;
    float wrongUntil;
    GameObject user;
    MouseLook look;
    PlayerMovement move;
    Renderer rend;
    Material screenMat;
    Font font;
    Texture2D dimTex;

    void Awake()
    {
        // A gated level starts locked until its question is answered.
        if (lockExitUntilSolved)
            G1LevelExitTrigger.ElevatorUnlocked = false;
    }

    void Start()
    {
        font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
        dimTex = new Texture2D(1, 1);
        dimTex.SetPixel(0, 0, Color.black);
        dimTex.Apply();

        rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            screenMat = new Material(rend.sharedMaterial);
            rend.sharedMaterial = screenMat;
            SetScreen(new Color(0.85f, 0.15f, 0.15f));   // locked red
        }
    }

    void SetScreen(Color c)
    {
        if (screenMat == null) return;
        screenMat.color = c;
        if (screenMat.HasProperty("_EmissionColor"))
        {
            screenMat.SetColor("_EmissionColor", c * 0.4f);
            screenMat.EnableKeyword("_EMISSION");
        }
    }

    public void OnUse(GameObject u)
    {
        if (solved)
        {
            u.GetComponent<PlayerHUD>()?.ShowTerminalLog("CONSOLE: ALREADY SOLVED — THE WAY IS OPEN.");
            return;
        }
        user = u;
        panelOpen = true;
        look = FindFirstObjectByType<MouseLook>();
        move = FindFirstObjectByType<PlayerMovement>();
        if (look) look.enabled = false;
        if (move) move.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        G1Audio.Play2D("pickup", 0.4f, 1.1f);
    }

    void Close()
    {
        panelOpen = false;
        if (look) look.enabled = true;
        if (move) move.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Answer(int i)
    {
        if (i == correctIndex)
        {
            solved = true;
            SetScreen(new Color(0.2f, 0.9f, 0.4f));   // solved green
            G1LevelExitTrigger.ElevatorUnlocked = true;
            G1Audio.Play2D("pickup", 0.6f, 1.4f);
            user?.GetComponent<PlayerHUD>()?.ShowTerminalLog("CONSOLE: CORRECT — OVERRIDE ACCEPTED. THE WAY IS OPEN.");
            if (!string.IsNullOrEmpty(objectiveId) && G1ObjectiveManager.Instance != null)
                G1ObjectiveManager.Instance.CompleteObjective(objectiveId);
            if (targetDoor != null)
                targetDoor.OnUse(gameObject);
            Close();
        }
        else
        {
            wrongFlash = true;
            wrongUntil = Time.time + 1.6f;
            G1Audio.Play2D("hit_thunk", 0.5f, 0.8f);
        }
    }

    void OnGUI()
    {
        if (!panelOpen) return;

        // Dim the world behind the modal.
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), dimTex);
        GUI.color = Color.white;

        float w = Mathf.Min(720f, Screen.width * 0.82f);
        float h = 340f;
        float x = (Screen.width - w) / 2f;
        float y = (Screen.height - h) / 2f;
        GUI.Box(new Rect(x, y, w, h), "");

        var qStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true, alignment = TextAnchor.UpperLeft };
        if (font) qStyle.font = font;
        qStyle.normal.textColor = new Color(1f, 0.75f, 0.12f);
        GUI.Label(new Rect(x + 24, y + 20, w - 48, 120), "SECURITY CONSOLE — ANSWER TO PROCEED\n\n" + question, qStyle);

        var bStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
        if (font) bStyle.font = font;
        for (int i = 0; i < options.Length; i++)
            if (GUI.Button(new Rect(x + 24, y + 150 + i * 50, w - 48, 42), $"   {(char)('A' + i)}.   {options[i]}", bStyle))
                Answer(i);

        if (wrongFlash && Time.time < wrongUntil)
        {
            var wStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            wStyle.normal.textColor = new Color(1f, 0.3f, 0.25f);
            GUI.Label(new Rect(x, y + h - 32, w, 24), "ACCESS DENIED — TRY AGAIN", wStyle);
        }

        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            Close();
    }
}
