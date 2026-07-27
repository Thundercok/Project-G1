using UnityEngine;

/// Keeps hand-authored coordinates honest against the Blender-generated map.
///
/// The Sprawl is a handful of merged meshes, so nothing can be queried by
/// object — the only way to know whether a spot is standable is to probe it.
/// A contact needs three things: solid ground near y=0 (not a rooftop), no
/// geometry occupying the body, and open sky overhead (every building on this
/// map is a sealed block, so a ceiling means we're inside one with no door).
/// When a desired spot fails, we spiral outwards and take the nearest that
/// passes, so a placement can be off by a few metres without trapping anyone.
public static class G1Placement
{
    const float BodyRadius = 0.45f;
    const float BodyHeight = 1.8f;
    const float MaxGroundY = 1.5f;      // above this we're standing on something
    const float SkyProbe = 60f;

    /// Scene builders create a collider and move it in the same editor frame,
    /// and until physics is told, every query still sees it at the origin. Each
    /// public entry point pays for one sync rather than trusting callers to
    /// remember — getting this wrong makes placement quietly probe an empty
    /// world and approve spots inside buildings.
    static void Sync() => Physics.SyncTransforms();

    /// Nearest spot to `desired` a person can stand and be reached. Logs when
    /// it has to move one, so bad coordinates surface instead of hiding.
    public static Vector3 FindStandingSpot(Vector3 desired, string label,
                                           float searchRadius = 40f)
    {
        Sync();
        if (IsStandable(desired, out Vector3 snapped))
            return snapped;

        for (float r = 4f; r <= searchRadius; r += 4f)
        {
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                var probe = desired + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (IsStandable(probe, out Vector3 ok))
                {
                    Debug.Log($"G1: {label} moved {r:0}m " +
                              $"({desired.x:0},{desired.z:0}) -> ({ok.x:0},{ok.z:0}) " +
                              "— original spot was blocked or enclosed.");
                    return ok;
                }
            }
        }
        Debug.LogWarning($"G1: no standable spot within {searchRadius}m of {label} " +
                         $"at {desired} — leaving it where it is.");
        return desired;
    }

    /// Nearest spot where a building of `half` extents sits on clear, flat
    /// ground. Used for the free-standing bunkers, which must not grow out of
    /// the side of a warehouse.
    public static Vector3 FindClearFootprint(Vector3 desired, Vector2 half, string label,
                                             float searchRadius = 45f)
    {
        Sync();
        if (FootprintClear(desired, half)) return desired;

        for (float r = 6f; r <= searchRadius; r += 6f)
        {
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                var probe = desired + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (FootprintClear(probe, half))
                {
                    Debug.Log($"G1: {label} moved {r:0}m to clear ground " +
                              $"({desired.x:0},{desired.z:0}) -> ({probe.x:0},{probe.z:0}).");
                    return probe;
                }
            }
        }
        Debug.LogWarning($"G1: no clear footprint within {searchRadius}m of {label}.");
        return desired;
    }

    public static bool FootprintClear(Vector3 centre, Vector2 half)
    {
        for (float dx = -half.x; dx <= half.x; dx += half.x)
            for (float dz = -half.y; dz <= half.y; dz += half.y)
            {
                var p = centre + new Vector3(dx, 0f, dz);
                if (!Physics.Raycast(new Vector3(p.x, SkyProbe, p.z), Vector3.down,
                                     out RaycastHit g, SkyProbe * 2f, ~0,
                                     QueryTriggerInteraction.Ignore)) return false;
                if (Mathf.Abs(g.point.y) > 0.8f) return false;      // slope or rooftop
                if (Physics.Raycast(p + Vector3.up * 0.3f, Vector3.up, 6f, ~0,
                                    QueryTriggerInteraction.Ignore)) return false;
            }
        return true;
    }

    /// Picks the facing whose doorway has clear ground to walk in from, so a
    /// structure never opens straight into a wall. Returns the preferred yaw
    /// unchanged when it already works.
    public static float BestDoorYaw(Vector3 centre, float halfDepth, float preferredYaw,
                                    string label)
    {
        Sync();
        float[] candidates = { preferredYaw, preferredYaw + 90f, preferredYaw + 180f,
                               preferredYaw + 270f };
        foreach (float yaw in candidates)
        {
            // the doorway sits on the -Z face, so the approach is behind it
            Vector3 outward = Quaternion.Euler(0f, yaw, 0f) * Vector3.back;
            bool clear = true;
            for (float d = halfDepth + 1f; d <= halfDepth + 7f; d += 2f)
                if (!IsStandable(centre + outward * d, out _)) { clear = false; break; }

            if (clear)
            {
                if (!Mathf.Approximately(yaw, preferredYaw))
                    Debug.Log($"G1: {label} door turned to yaw {yaw % 360f} — " +
                              $"{preferredYaw % 360f} opened into blocked ground.");
                return yaw;
            }
        }
        Debug.LogWarning($"G1: no clear approach to {label}'s door; keeping {preferredYaw}.");
        return preferredYaw;
    }

    /// Human-readable reason a spot fails, for build reports.
    public static string Describe(Vector3 at)
    {
        if (!Physics.Raycast(new Vector3(at.x, SkyProbe, at.z), Vector3.down,
                             out RaycastHit ground, SkyProbe * 2f, ~0,
                             QueryTriggerInteraction.Ignore))
            return "no ground below";
        if (ground.point.y > MaxGroundY)
            return $"ground is a rooftop at y={ground.point.y:0.0} ({ground.collider.name})";

        Vector3 snapped = new Vector3(at.x, ground.point.y + 0.1f, at.z);
        Vector3 foot = snapped + Vector3.up * (BodyRadius + 0.05f);
        Vector3 head = snapped + Vector3.up * (BodyHeight - BodyRadius);
        var hits = Physics.OverlapCapsule(foot, head, BodyRadius, ~0,
                                          QueryTriggerInteraction.Ignore);
        if (hits.Length > 0)
        {
            var names = new System.Text.StringBuilder();
            foreach (var h in hits) names.Append(h.name).Append(' ');
            return "body blocked by: " + names;
        }

        if (Physics.Raycast(snapped + Vector3.up * BodyHeight, Vector3.up,
                            out RaycastHit ceil, SkyProbe, ~0,
                            QueryTriggerInteraction.Ignore))
            return $"roofed over by {ceil.collider.name} at y={ceil.point.y:0.0}";

        return "clear";
    }

    /// True when someone could stand here and be walked up to. Outputs the
    /// position snapped down onto the ground.
    public static bool IsStandable(Vector3 at, out Vector3 snapped)
    {
        snapped = at;

        // 1. ground, and at roughly map level rather than on a roof
        if (!Physics.Raycast(new Vector3(at.x, SkyProbe, at.z), Vector3.down,
                             out RaycastHit ground, SkyProbe * 2f, ~0,
                             QueryTriggerInteraction.Ignore))
            return false;
        if (ground.point.y > MaxGroundY)
            return false;
        snapped = new Vector3(at.x, ground.point.y + 0.1f, at.z);

        // 2. nothing occupying the body
        Vector3 foot = snapped + Vector3.up * (BodyRadius + 0.05f);
        Vector3 head = snapped + Vector3.up * (BodyHeight - BodyRadius);
        if (Physics.CheckCapsule(foot, head, BodyRadius, ~0,
                                 QueryTriggerInteraction.Ignore))
            return false;

        // 3. open sky — a ceiling on this map means a sealed block
        if (Physics.Raycast(snapped + Vector3.up * BodyHeight, Vector3.up,
                            SkyProbe, ~0, QueryTriggerInteraction.Ignore))
            return false;

        return true;
    }
}
