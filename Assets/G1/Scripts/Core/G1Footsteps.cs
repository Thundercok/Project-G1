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

        bool sprint = speed > 6f;
        float vol = move.IsCrouching ? 0.2f : (sprint ? 0.42f : 0.32f);
        float pitch = sprint ? 1.2f : 1f;
        variant = (variant + 1) % 4;
        G1Audio.Play2D("step_concrete_" + variant, vol, pitch, 0.05f);
    }
}
