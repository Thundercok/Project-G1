using UnityEngine;

/// CombatArenaMonitor — shared battlefield telemetry, zero-alloc poll.
public sealed class CombatArenaMonitor : MonoBehaviour
{
    public static CombatArenaMonitor Instance { get; private set; }

    public Transform player;
    public HealthSystem playerHealth;
    public float engagementRadius = 5f;
    public float pollRate = 0.3f;

    public int MobsEngagingPlayer { get; private set; }
    public float PlayerHealthPct 
    {
        get
        {
            if (playerHealth != null && playerHealth.maxHealth > 0f)
                return playerHealth.CurrentHealth / playerHealth.maxHealth;
            return 1f;
        }
    }

    private readonly Collider[] _buf = new Collider[16];
    private int _mobMask;
    private float _nextPoll;

    private void Awake() 
    { 
        if (Instance != null) { Destroy(gameObject); return; } 
        Instance = this; 

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer == -1) enemyLayer = 10;
        _mobMask = 1 << enemyLayer;
    }

    private void Update()
    {
        if (player == null) return;
        if (Time.time < _nextPoll) return;
        _nextPoll = Time.time + pollRate;

        MobsEngagingPlayer = Physics.OverlapSphereNonAlloc(
            player.position, engagementRadius, _buf, _mobMask);
    }
}
