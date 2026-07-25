using System.Collections;
using UnityEngine;

/// <summary>
/// Cinematic encounter trigger for the Industrial Hall entrance (Level 1).
/// Freezes 3 HECU soldiers, turns them slowly toward player, plays radio barks,
/// holds an awkward pause, and triggers combat.
/// </summary>
public class G1HECUEncounterTrigger : MonoBehaviour
{
    public G1SoldierAI[] soldiers;
    private bool hasTriggered = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSetupInScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene == null || string.IsNullOrEmpty(scene.name)) return;
        if (!scene.name.Contains("Test") && !scene.name.Contains("Level1")) return;

        if (GameObject.Find("EncounterTrigger_IndustrialHall") == null)
        {
            var triggerGo = new GameObject("EncounterTrigger_IndustrialHall");
            triggerGo.transform.position = new Vector3(12f, 1.5f, 29f);
            var boxCol = triggerGo.AddComponent<BoxCollider>();
            boxCol.isTrigger = true;
            boxCol.size = new Vector3(20f, 4f, 2f);
            var triggerComp = triggerGo.AddComponent<G1HECUEncounterTrigger>();

            var s1 = GameObject.Find("HECU_Suppress")?.GetComponent<G1SoldierAI>();
            var s2 = GameObject.Find("HECU_FlankLeft")?.GetComponent<G1SoldierAI>();
            var s3 = GameObject.Find("HECU_FlankRight")?.GetComponent<G1SoldierAI>();
            var list = new System.Collections.Generic.List<G1SoldierAI>();
            if (s1) list.Add(s1);
            if (s2) list.Add(s2);
            if (s3) list.Add(s3);
            triggerComp.soldiers = list.ToArray();
            foreach (var s in list) s.encounterFrozen = true;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasTriggered || !other.CompareTag("Player"))
            return;

        hasTriggered = true;
        StartCoroutine(PlayEncounterSequence(other.gameObject));
    }

    IEnumerator PlayEncounterSequence(GameObject player)
    {
        // Find soldiers if array empty
        if (soldiers == null || soldiers.Length == 0)
        {
            var s1 = GameObject.Find("HECU_Suppress")?.GetComponent<G1SoldierAI>();
            var s2 = GameObject.Find("HECU_FlankLeft")?.GetComponent<G1SoldierAI>();
            var s3 = GameObject.Find("HECU_FlankRight")?.GetComponent<G1SoldierAI>();
            var list = new System.Collections.Generic.List<G1SoldierAI>();
            if (s1) list.Add(s1);
            if (s2) list.Add(s2);
            if (s3) list.Add(s3);
            soldiers = list.ToArray();
        }

        // 1. Freeze soldiers AI and player controls
        if (soldiers != null)
        {
            foreach (var s in soldiers)
            {
                if (s != null) s.encounterFrozen = true;
            }
        }

        var move = player.GetComponent<PlayerMovement>();
        var look = player.GetComponentInChildren<MouseLook>();

        if (move) move.enabled = false;
        if (look) look.enabled = false;

        if (G1CutsceneManager.Instance != null)
            G1CutsceneManager.Instance.isCutsceneActive = true;

        // 2. Play radio static and show subtitle
        G1Audio.Play2D("radio_static", 0.8f);
        if (G1CutsceneManager.Instance != null)
            G1CutsceneManager.Instance.ShowSubtitle("[HECU RADIO]: *click* ...Wait. Movement at the access door.", 2.5f);

        // 3. Slowly turn soldiers toward player over 1.2 seconds
        float elapsed = 0f;
        Vector3 playerPos = player.transform.position;

        Quaternion[] startRots = new Quaternion[soldiers != null ? soldiers.Length : 0];
        Quaternion[] targetRots = new Quaternion[soldiers != null ? soldiers.Length : 0];

        if (soldiers != null)
        {
            for (int i = 0; i < soldiers.Length; i++)
            {
                if (soldiers[i] != null)
                {
                    startRots[i] = soldiers[i].transform.rotation;
                    Vector3 lookDir = (playerPos - soldiers[i].transform.position).normalized;
                    lookDir.y = 0f;
                    if (lookDir != Vector3.zero)
                        targetRots[i] = Quaternion.LookRotation(lookDir);
                    else
                        targetRots[i] = startRots[i];
                }
            }
        }

        while (elapsed < 1.2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 1.2f;

            if (soldiers != null)
            {
                for (int i = 0; i < soldiers.Length; i++)
                {
                    if (soldiers[i] != null)
                        soldiers[i].transform.rotation = Quaternion.Slerp(startRots[i], targetRots[i], t);
                }
            }
            yield return null;
        }

        // 4. Awkward Pause — Silence & Stare
        yield return new WaitForSeconds(0.6f);

        if (G1CutsceneManager.Instance != null)
            G1CutsceneManager.Instance.ShowSubtitle("[HECU SOLDIER]: ...Is that... the test subject from Sector C?", 2.5f);

        G1Audio.Play2D("pickup", 0.5f, 0.7f);
        yield return new WaitForSeconds(1.2f);

        // 5. Command to attack!
        if (G1CutsceneManager.Instance != null)
            G1CutsceneManager.Instance.ShowSubtitle("[HECU COMMAND]: WEAPONS FREE! ENGAGE TARGET!", 2.0f);

        G1Audio.Play2D("radio_static", 0.9f);
        yield return new WaitForSeconds(0.4f);

        // 6. Restore controls & activate soldiers
        if (move) move.enabled = true;
        if (look) look.enabled = true;

        if (G1CutsceneManager.Instance != null)
            G1CutsceneManager.Instance.isCutsceneActive = false;

        if (soldiers != null)
        {
            foreach (var s in soldiers)
            {
                if (s != null)
                {
                    s.encounterFrozen = false;
                    s.ForceAlertAt(playerPos);
                }
            }
        }

        var col = GetComponent<Collider>();
        if (col) col.enabled = false;
    }
}
