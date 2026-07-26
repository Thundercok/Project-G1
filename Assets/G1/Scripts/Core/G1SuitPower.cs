using UnityEngine;

/// The HEV suit's auxiliary power cell — the budget that sprinting spends.
///
/// A flat speed buff on Shift would just be a faster walk with no decision in
/// it. Metering it means every sprint is a choice about whether you want the
/// distance now or the escape later, which is the whole reason HL2 put a bar
/// on it. The numbers here are tuned for an 800m battlefield rather than a
/// corridor: roughly seven seconds of sprint, five to refill.
public sealed class G1SuitPower : MonoBehaviour
{
    [Header("Reserve")]
    public float maxPower = 100f;
    public float drainPerSecond = 14f;      // ~7.1s of continuous sprint
    public float regenPerSecond = 20f;      // ~5s back to full
    public float regenDelay = 0.6f;         // beat before the cell recovers

    /// Tapping Shift on an empty cell would grant one frame of sprint per
    /// press, strobing the FOV and the footstep pitch. So a run has to *start*
    /// with this much in reserve — but once it is running it may drain the
    /// cell all the way down, which is what makes the last second of a sprint
    /// feel like it is costing you something.
    public float minimumToStart = 12f;

    float power;
    float regenBlockedUntil;
    bool drainedThisFrame;      // reset every LateUpdate
    bool sprintOngoing;         // survives across frames of one continuous run

    public float Power => power;
    public float Fraction => maxPower > 0f ? power / maxPower : 0f;
    public bool Draining => sprintOngoing;
    /// True while the cell is locked out — the HUD reads this to warn you.
    public bool Depleted => power < minimumToStart && !sprintOngoing;

    void Awake() => power = maxPower;

    void LateUpdate()
    {
        // Consumers call TryDrain() during Update; a frame that nobody claimed
        // ends the run and starts the cell recovering.
        if (!drainedThisFrame)
        {
            sprintOngoing = false;
            if (Time.time >= regenBlockedUntil && power < maxPower)
                power = Mathf.Min(maxPower, power + regenPerSecond * Time.deltaTime);
        }
        drainedThisFrame = false;
    }

    /// Spend one frame's worth of power. Returns false when the cell can't
    /// support the draw, so the caller drops back to walking speed.
    public bool TryDrain(float dt)
    {
        float floor = sprintOngoing ? 0f : minimumToStart;
        if (power <= floor) return false;

        power = Mathf.Max(0f, power - drainPerSecond * dt);
        regenBlockedUntil = Time.time + regenDelay;
        drainedThisFrame = true;
        sprintOngoing = true;
        return true;
    }

    public void Recharge(float amount)
    {
        power = Mathf.Clamp(power + amount, 0f, maxPower);
    }
}
