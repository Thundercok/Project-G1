using UnityEngine;

/// Editor-only AI state visualization for HECU soldiers: a colored wire sphere
/// per combat action, a state/role label overhead, a green line to the claimed
/// cover point and a red line to the player target.
///
/// Note: lives in the runtime assembly (not an Editor/ folder) because it must
/// attach to the soldier prefab — all editor-only drawing is compiled out of
/// player builds via UNITY_EDITOR.
public sealed class AIDebugGizmos : MonoBehaviour
{
#if UNITY_EDITOR
    G1SoldierAI ai;

    static Color ColorFor(G1SoldierAI.CombatAction action)
    {
        switch (action)
        {
            case G1SoldierAI.CombatAction.PopFire: return Color.yellow;
            case G1SoldierAI.CombatAction.MoveToCover: return Color.cyan;
            case G1SoldierAI.CombatAction.Suppress: return new Color(1f, 0.5f, 0f);
            case G1SoldierAI.CombatAction.Flank: return Color.magenta;
            case G1SoldierAI.CombatAction.Reload: return Color.blue;
            case G1SoldierAI.CombatAction.Retreat: return Color.grey;
            case G1SoldierAI.CombatAction.Opportunist: return Color.red;
            default: return Color.white;
        }
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying)
            return;
        if (!ai)
        {
            ai = GetComponent<G1SoldierAI>();
            if (!ai)
                return;
        }

        Vector3 head = transform.position + Vector3.up * 2.35f;
        Gizmos.color = ColorFor(ai.CurrentAction);
        Gizmos.DrawWireSphere(head, 0.15f);
        UnityEditor.Handles.Label(head + Vector3.up * 0.25f,
            $"{ai.CurrentAction} | {ai.ClaimedRole}");

        Vector3 chest = transform.position + Vector3.up * 1.2f;
        if (ai.ClaimedCover)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(chest, ai.ClaimedCover.transform.position);
        }
        if (ai.PlayerXform)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(chest, ai.PlayerXform.position + Vector3.up * 1.2f);
        }
    }
#endif
}
