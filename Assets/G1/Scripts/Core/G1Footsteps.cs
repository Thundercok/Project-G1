using UnityEngine;

/// Speed-scaled concrete footsteps for the player: round-robin through the
/// four synthesized variants; quieter crouched, faster+brighter sprinting.
[RequireComponent(typeof(PlayerMovement))]
public sealed class G1Footsteps : MonoBehaviour
{
    public float baseInterval = 0.45f;

    PlayerMovement move;
    float nextStep;
    int variant;

    void Awake()
    {
        move = GetComponent<PlayerMovement>();
    }

    void Update()
    {
        Vector3 hv = move.Velocity;
        hv.y = 0f;
        float speed = hv.magnitude;
        if (!move.Grounded || speed < 0.5f)
            return;

        if (Time.time < nextStep)
            return;
        nextStep = Time.time + baseInterval / Mathf.Max(0.3f, speed / move.maxSpeed);

        // 6 m/s was below the 8.1 walk speed, so every step already sounded
        // like a run. Ask the mover instead of guessing from velocity.
        bool sprint = move.IsSprinting || speed > move.maxSpeed * 1.15f;
        float vol = move.IsCrouching ? 0.2f : (sprint ? 0.42f : 0.32f);
        float pitch = sprint ? 1.2f : 1f;

        // a boot on a concrete floor indoors is louder and duller than the same
        // boot on open ground — the listener's reverb does the tail, this does
        // the strike
        if (G1InteriorSpace.PlayerIsIndoors) { vol *= 1.25f; pitch *= 0.92f; }
        variant = (variant + 1) % 4;
        G1Audio.Play2D("step_concrete_" + variant, vol, pitch, 0.05f);
    }
}
