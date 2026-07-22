using UnityEngine;

/// HECU radio chatter: reads the soldier's GOAP action (no core-AI changes)
/// and squelches a bark on tactical transitions and on death. A shared
/// cooldown keeps the whole squad from talking over itself.
[RequireComponent(typeof(G1SoldierAI))]
public sealed class G1SoldierBarks : MonoBehaviour
{
    static float nextBarkTime;      // squad-wide throttle

    G1SoldierAI ai;
    G1SoldierAI.CombatAction lastAction;

    void Start()
    {
        ai = GetComponent<G1SoldierAI>();
        lastAction = ai.CurrentAction;
        var hs = GetComponent<HealthSystem>();
        if (hs != null)
            hs.OnDeath += (p, n) => Bark("radio_bark_b", 0.9f);   // "man down"
    }

    void Update()
    {
        var a = ai.CurrentAction;
        if (a != lastAction)
        {
            if (a == G1SoldierAI.CombatAction.Flank ||
                a == G1SoldierAI.CombatAction.Suppress ||
                a == G1SoldierAI.CombatAction.Opportunist)
                Bark("radio_bark_a", 1f);       // "flanking / suppressing"
            lastAction = a;
        }
    }

    void Bark(string clip, float pitch)
    {
        if (Time.time < nextBarkTime)
            return;
        nextBarkTime = Time.time + 2.2f;
        G1Audio.Play(clip, transform.position, 0.7f, pitch, 0.08f);
    }
}
