using System.Collections.Generic;
using UnityEngine;

public sealed class G1CoverPoint : MonoBehaviour
{
    public bool Claimed;
    private static readonly List<G1CoverPoint> _registry = new List<G1CoverPoint>(32);
    private static readonly int _wallMask = 1 << 0; // Default layer obstacles

    // Validation Cache to prevent main-thread bottleneck
    private Vector3 _lastThreatPos;
    private bool _lastValidationResult;
    private float _lastValidationTime = -99f;
    private const float CacheDuration = 0.25f;
    private const float MaxThreatMoveSqr = 1.0f; // 1 meter squared

    private void OnEnable()  => _registry.Add(this);
    private void OnDisable() => _registry.Remove(this);

    public static G1CoverPoint FindNearestValid(Vector3 from, Vector3 threatPos, float maxDist)
    {
        G1CoverPoint best = null;
        float bestSqr = maxDist * maxDist;

        for (int i = 0; i < _registry.Count; i++)
        {
            var cp = _registry[i];
            if (cp == null || cp.Claimed) continue;

            float sqr = (cp.transform.position - from).sqrMagnitude;
            if (sqr > bestSqr) continue;

            // Check cache or validate dynamically
            if (Time.time - cp._lastValidationTime < CacheDuration && 
                (threatPos - cp._lastThreatPos).sqrMagnitude < MaxThreatMoveSqr)
            {
                if (!cp._lastValidationResult) continue;
            }
            else
            {
                cp._lastThreatPos = threatPos;
                cp._lastValidationTime = Time.time;
                cp._lastValidationResult = Validate(cp.transform.position, threatPos);
                if (!cp._lastValidationResult) continue;
            }

            bestSqr = sqr;
            best = cp;
        }
        return best;
    }

    // Hidden when crouched, exposed when standing to pop out and shoot
    private static bool Validate(Vector3 point, Vector3 threatPos)
    {
        Vector3 crouchEye = point + Vector3.up * 0.35f;
        Vector3 standEye  = point + Vector3.up * 1.5f;

        bool hiddenCrouched = Physics.Linecast(crouchEye, threatPos, _wallMask);
        bool exposedStanding = !Physics.Linecast(standEye, threatPos, _wallMask);

        return hiddenCrouched && exposedStanding;
    }
}
