using System.Collections.Generic;
using UnityEngine;

public enum SquadRole : byte { None, Suppress, FlankLeft, FlankRight }

public sealed class SquadBlackboard : MonoBehaviour
{
    public static SquadBlackboard Instance { get; private set; }
    private readonly Dictionary<SquadRole, G1SoldierAI> _claims = new Dictionary<SquadRole, G1SoldierAI>();

    private readonly List<G1SoldierAI> _opportunists = new List<G1SoldierAI>(4);
    public int OpportunistCount => _opportunists.Count;

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

    private bool _alphaStrikeFired;

    public void RegisterOpportunist(G1SoldierAI s)   { if (!_opportunists.Contains(s)) _opportunists.Add(s); }
    public void UnregisterOpportunist(G1SoldierAI s) => _opportunists.Remove(s);

    public float AlphaStrikeTimestamp { get; private set; } = -1f;
    
    public void TriggerAlphaStrike()
    {
        if (_alphaStrikeFired) return;
        _alphaStrikeFired = true;
        AlphaStrikeTimestamp = Time.time;
    }

    public void ResetAlphaStrike()
    {
        _alphaStrikeFired = false;
        AlphaStrikeTimestamp = -1f;
    }
}
