using UnityEngine;
using UnityEngine.SceneManagement;

/// HECU radio chatter: reads the soldier's GOAP action (no core-AI changes)
/// and squelches a bark on tactical transitions and on death. A shared cooldown
/// keeps the whole squad from talking over itself.
///
/// On top of the audio squelch it now surfaces a subtitle line that DEGRADES by
/// level tier — professional cleanup chatter on Level 1, unease on Level 2, open
/// dread on Level 3 — from G1LoreText.
[RequireComponent(typeof(G1SoldierAI))]
public sealed class G1SoldierBarks : MonoBehaviour
{
    static float nextBarkTime;      // squad-wide throttle
    static float nextTextTime;      // subtitle throttle (rarer than audio)

    G1SoldierAI ai;
    G1SoldierAI.CombatAction lastAction;
    int tier = 1;

    void Start()
    {
        ai = GetComponent<G1SoldierAI>();
        lastAction = ai.CurrentAction;
        tier = TierForScene(SceneManager.GetActiveScene().name);

        var hs = GetComponent<HealthSystem>();
        if (hs != null)
            hs.OnDeath += (p, n) => Bark("radio_bark_b", 0.9f, true);   // "man down"
    }

    void Update()
    {
        var a = ai.CurrentAction;
        if (a != lastAction)
        {
            if (a == G1SoldierAI.CombatAction.Flank ||
                a == G1SoldierAI.CombatAction.Suppress ||
                a == G1SoldierAI.CombatAction.Opportunist)
                Bark("radio_bark_a", 1f, true);       // "flanking / suppressing"
            lastAction = a;
        }
    }

    void Bark(string clip, float pitch, bool withText = false)
    {
        if (Time.time < nextBarkTime)
            return;
        nextBarkTime = Time.time + 2.2f;
        G1Audio.Play(clip, transform.position, 0.7f, pitch, 0.08f);

        // Occasionally attach a spoken (subtitled) line, throttled so the squad
        // doesn't monologue over itself.
        if (withText && Time.time >= nextTextTime && G1CutsceneManager.Instance != null)
        {
            nextTextTime = Time.time + 7f;
            G1CutsceneManager.Instance.ShowSubtitle(
                "[SWEEPER RADIO]: \"" + G1LoreText.NextBark(tier) + "\"", 4.5f);
        }
    }

    static int TierForScene(string name)
    {
        if (name.Contains("Level3")) return 3;
        if (name.Contains("Level2")) return 2;
        return 1;   // TestScene / Level 1 and everything else
    }
}
