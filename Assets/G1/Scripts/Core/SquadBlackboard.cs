using System.Collections.Generic;
using UnityEngine;

public enum SquadRole : byte { None, Suppress, FlankLeft, FlankRight }

public sealed class SquadBlackboard : MonoBehaviour
{
    public static SquadBlackboard Instance { get; private set; }
    private readonly Dictionary<SquadRole, G1SoldierAI> _claims = new Dictionary<SquadRole, G1SoldierAI>();

    private void Awake() 
    { 
        if (Instance != null) 
        { 
            Destroy(gameObject); 
            return; 
        } 
        Instance = this; 
    }

    public bool TryClaim(SquadRole role, G1SoldierAI requester)
    {
        if (role == SquadRole.None) return true;

        if (_claims.TryGetValue(role, out var holder) && holder != null && holder != requester)
            return false;

        _claims[role] = requester;
        return true;
    }

    public void Release(SquadRole role, G1SoldierAI requester)
    {
        if (_claims.TryGetValue(role, out var holder) && holder == requester) 
        {
            _claims.Remove(role);
        }
    }

    public bool IsFree(SquadRole role) => !_claims.TryGetValue(role, out var h) || h == null;
}
