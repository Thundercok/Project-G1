using UnityEngine;

/// Auditor (G-Man style mysterious suit character) cutscene actor.
/// Turns to observe Chad Thundercock, triggers subtitles/dialogue, and vanishes when approached.
public class G1AuditorCutsceneActor : MonoBehaviour
{
    [Header("Auditor Settings")]
    public string dialogLine = "Subject: Chad Thundercock... anomalous potential detected.";
    public float triggerRadius = 8.0f;
    public float vanishRadius = 4.0f;
    public bool vanishOnClose = true;

    private Transform playerTransform;
    private bool hasTriggeredLine = false;
    private bool isVanishing = false;
    private float alpha = 1.0f;
    private Renderer[] renderers;

    private void Start()
    {
        renderers = GetComponentsInChildren<Renderer>();
        var player = GameObject.FindWithTag("Player");
        if (player != null) playerTransform = player.transform;
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) playerTransform = player.transform;
            return;
        }

        float dist = Vector3.Distance(transform.position, playerTransform.position);

        // Turn to watch Chad Thundercock
        Vector3 targetDir = (playerTransform.position - transform.position);
        targetDir.y = 0;
        if (targetDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(targetDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 3.5f);
        }

        // Trigger subtitle line on approach
        if (dist <= triggerRadius && !hasTriggeredLine)
        {
            hasTriggeredLine = true;
            if (G1CutsceneManager.Instance != null && !string.IsNullOrEmpty(dialogLine))
            {
                G1CutsceneManager.Instance.ShowSubtitle($"[THE AUDITOR]: \"{dialogLine}\"", 5.0f);
            }
        }

        // Vanish into thin air if player gets too close
        if (dist <= vanishRadius && vanishOnClose && !isVanishing)
        {
            isVanishing = true;
        }

        if (isVanishing)
        {
            alpha -= Time.deltaTime * 2.5f;
            foreach (var r in renderers)
            {
                if (r != null && r.material != null)
                {
                    Color c = r.material.color;
                    c.a = Mathf.Clamp01(alpha);
                    r.material.color = c;
                }
            }
            if (alpha <= 0.05f)
            {
                Destroy(gameObject);
            }
        }
    }
}
