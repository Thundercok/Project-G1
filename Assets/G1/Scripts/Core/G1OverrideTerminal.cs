using UnityEngine;

public class G1OverrideTerminal : MonoBehaviour, IUsable
{
    public SlidingDoor targetDoor;
    public string lockedMessage = "CONSOLE: EMERGENCY OVERRIDE LOCKED. PURGE CORES (ELIMINATE SPECIMENS) FIRST.";
    public string unlockedMessage = "CONSOLE: EMERGENCY OVERRIDE READY. PRESS USE TO ACTIVATE.";
    
    [HideInInspector]
    public bool isUnlocked = false;
    private bool _used = false;

    private Renderer _renderer;
    private Material _screenMat;

    private void Start()
    {
        _renderer = GetComponentInChildren<Renderer>();
        if (_renderer != null)
        {
            // Create instance material so we can toggle color without modifying original asset
            _screenMat = new Material(_renderer.sharedMaterial);
            _renderer.sharedMaterial = _screenMat;
            UpdateScreenColor();
        }
    }

    private void Update()
    {
        if (isUnlocked && !_used)
        {
            UpdateScreenColor();
        }
    }

    private void UpdateScreenColor()
    {
        if (_screenMat != null)
        {
            if (_used)
            {
                _screenMat.color = new Color(0.2f, 0.9f, 0.4f); // active green
                _screenMat.SetColor("_EmissionColor", new Color(0.05f, 0.35f, 0.1f));
            }
            else if (isUnlocked)
            {
                _screenMat.color = new Color(0.1f, 0.8f, 0.8f); // teal ready
                _screenMat.SetColor("_EmissionColor", new Color(0.02f, 0.25f, 0.25f));
            }
            else
            {
                _screenMat.color = new Color(0.85f, 0.15f, 0.15f); // red locked
                _screenMat.SetColor("_EmissionColor", new Color(0.35f, 0.05f, 0.05f));
            }
            _screenMat.EnableKeyword("_EMISSION");
        }
    }

    public void OnUse(GameObject user)
    {
        var hud = user.GetComponent<PlayerHUD>();
        if (hud == null) return;

        if (!isUnlocked)
        {
            hud.ShowTerminalLog(lockedMessage);
            G1Audio.Play2D("hit_thunk", 0.45f, 0.85f);
            return;
        }

        if (_used) return;
        _used = true;

        UpdateScreenColor();
        hud.ShowTerminalLog("CONSOLE: OVERRIDE SUCCESSFUL. ELEVATOR POWER RESTORED.");
        G1Audio.Play2D("pickup", 0.5f, 1.4f);

        // Unlock elevator
        G1LevelExitTrigger.ElevatorUnlocked = true;

        if (G1ObjectiveManager.Instance != null)
        {
            G1ObjectiveManager.Instance.CompleteObjective("override_terminal");
        }

        // Open target door (Door 4)
        if (targetDoor != null)
        {
            targetDoor.OnUse(gameObject);
        }
    }
}
