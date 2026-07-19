using System.Collections;
using UnityEngine;

/// Burst-fire Patrol Soldier AI.
/// Scans for player, patrols waypoints, and uses a F.E.A.R.-style GOAP-lite planner
/// to coordinate squad roles (blackboard claims), seek cover (dynamic cover points),
/// shoot in 3-shot bursts, drop supply packs, and perform opportunistic squad ambush strikes.
[RequireComponent(typeof(Animator), typeof(HealthSystem), typeof(UnityEngine.AI.NavMeshAgent))]
public class G1SoldierAI : MonoBehaviour
{
    private enum SoldierState : byte { Patrol, Alert, BurstFire, Dead }
    public enum CombatAction : byte { PopFire, MoveToCover, Suppress, Flank, Reload, Retreat, Opportunist }

    [Header("Patrol & Movement")]
    public Transform[] waypoints;
    public float patrolSpeed = 1.0f;
    public float chaseSpeed = 3.0f;
    public float turnSpeed = 220f;

    [Header("Combat & Detection")]
    public float detectRadius = 20f;
    public float attackRadius = 13f;
    public float damage = 8f;
    public float fireRate = 1.6f;        // cooldown between bursts
    public float burstGap = 0.11f;       // gap between shots in burst

    [Header("Opportunist Settings")]
    public int opportunistMobThreshold = 2;
    public float opportunistTimeout = 6f;
    public float executeHpThreshold = 0.5f;

    [Header("Drop Settings")]
    [Range(0f, 1f)] public float dropChance = 0.72f;

    private SoldierState state = SoldierState.Patrol;
    private HealthSystem myHealth;
    private Animator anim;
    private GameObject player;
    private HealthSystem playerHealth;
    private UnityEngine.AI.NavMeshAgent agent;

    private int waypointIdx;
    private float nextBurstTime;
    private float nextShotTime;
    private int shotsLeftInBurst;
    private bool playerSpotted;

    // GOAP-lite Variables
    private CombatAction _currentAction = CombatAction.PopFire;
    private G1CoverPoint _claimedCover;
    private SquadRole _claimedRole = SquadRole.None;

    // Read-only state exposure for observability tooling (AIDebugGizmos, HUDs)
    public CombatAction CurrentAction => _currentAction;
    public SquadRole ClaimedRole => _claimedRole;
    public G1CoverPoint ClaimedCover => _claimedCover;
    public Transform PlayerXform => player != null ? player.transform : null;
    private float _nextPlanTime;
    private const float PlanInterval = 0.4f;
    private bool _recentlyHit;
    private float _recentlyHitResetTime;

    private float _opportunistEnterTime;
    private float _lastAlphaStrikeSeen = -1f;

    // Zero-alloc buffers
    private readonly Collider[] detectBuf = new Collider[4];
    private int playerMask;
    private int obstacleMask;

    void Start()
    {
        myHealth = GetComponent<HealthSystem>();
        myHealth.OnDeath += HandleDeath;
        anim = GetComponent<Animator>();

        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        agent.updateRotation = false; // Rotate manually for smooth control

        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer == -1) playerLayer = 8;
        playerMask = 1 << playerLayer;
        obstacleMask = (1 << 0) | (1 << playerLayer);

        player = GameObject.FindWithTag("Player");
        if (player)
            playerHealth = player.GetComponent<HealthSystem>();

        // Auto-resolve waypoints from global path if none assigned
        if (waypoints == null || waypoints.Length == 0)
        {
            var patrolPathObj = GameObject.Find("SoldierPatrolPath");
            if (patrolPathObj != null)
            {
                waypoints = new Transform[patrolPathObj.transform.childCount];
                for (int i = 0; i < waypoints.Length; i++)
                {
                    waypoints[i] = patrolPathObj.transform.GetChild(i);
                }
            }
        }

        // Hook up hit notification
        myHealth.OnHealthChanged += (cur, max) =>
        {
            _recentlyHit = true;
            _recentlyHitResetTime = Time.time + 3.0f;
        };

        state = SoldierState.Patrol;
        waypointIdx = 0;
        
        // Start patrol animation
        bool isWalking = waypoints != null && waypoints.Length > 1;
        if (anim) anim.CrossFade(isWalking ? "Walk" : "Idle", 0.05f);
    }

    void Update()
    {
        if (agent && agent.enabled && !agent.isOnNavMesh)
            return;                     // not placed on a NavMesh (yet)
        if (state == SoldierState.Dead || player == null || playerHealth == null || playerHealth.IsDead)
            return;

        // Perform periodic player detection check
        PollDetection();

        // Decay hit registration
        if (_recentlyHit && Time.time > _recentlyHitResetTime)
        {
            _recentlyHit = false;
        }

        if (state == SoldierState.Patrol)
        {
            TickPatrol();
        }
        else
        {
            TickGOAP();
        }
    }

    void PollDetection()
    {
        playerSpotted = false;
        int count = Physics.OverlapSphereNonAlloc(transform.position, detectRadius, detectBuf, playerMask);
        
        for (int i = 0; i < count; i++)
        {
            if (detectBuf[i].CompareTag("Player"))
            {
                if (CheckLineOfSight())
                {
                    playerSpotted = true;
                    break;
                }
            }
        }

        if (playerSpotted && state == SoldierState.Patrol)
        {
            state = SoldierState.Alert;
            if (anim) anim.CrossFade("Walk", 0.1f);
        }
        else if (!playerSpotted && state != SoldierState.Patrol)
        {
            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist > detectRadius * 1.5f)
            {
                ResetPatrol();
            }
        }
    }

    bool CheckLineOfSight()
    {
        Vector3 eyePos = transform.position + Vector3.up * 1.5f + transform.forward * 0.45f;
        Vector3 targetPos = player.transform.position + Vector3.up * 1.5f;
        Vector3 dir = targetPos - eyePos;
        float dist = dir.magnitude;

        float angle = Vector3.Angle(transform.forward, dir.normalized);
        if (angle > 60f)
            return false;

        if (Physics.Raycast(eyePos, dir.normalized, out RaycastHit hit, dist, obstacleMask))
        {
            if (hit.collider.gameObject != player)
                return false;
        }
        return true;
    }

    void TickPatrol()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Vector3 target = waypoints[waypointIdx].position;
        target.y = transform.position.y;
        Vector3 to = target - transform.position;

        if (to.magnitude < 0.8f)
        {
            waypointIdx = (waypointIdx + 1) % waypoints.Length;
        }
        else
        {
            agent.speed = patrolSpeed;
            agent.SetDestination(target);

            Vector3 moveDir = agent.velocity;
            moveDir.y = 0f;
            if (moveDir.sqrMagnitude > 0.05f)
            {
                Quaternion look = Quaternion.LookRotation(moveDir.normalized);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
            }
            else if (to.sqrMagnitude > 0.01f)
            {
                Quaternion look = Quaternion.LookRotation(to.normalized);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
            }
        }
    }

    private void TickGOAP()
    {
        if (Time.time >= _nextPlanTime)
        {
            _nextPlanTime = Time.time + PlanInterval;
            _currentAction = ScoreActions();
        }
        ExecuteAction(_currentAction);
    }

    private CombatAction ScoreActions()
    {
        float hpPct = myHealth.CurrentHealth / myHealth.maxHealth;
        if (hpPct < 0.25f)
        {
            ReleaseCover();
            ReleaseRole();
            return CombatAction.Retreat;
        }

        if (shotsLeftInBurst <= 0 && Time.time > nextBurstTime && Random.value < 0.3f)
        {
            ReleaseCover();
            ReleaseRole();
            return CombatAction.Reload;
        }

        // --- Opportunistic Predator logic ---
        var arena = CombatArenaMonitor.Instance;
        if (arena != null && SquadBlackboard.Instance != null)
        {
            bool overwhelmed = arena.MobsEngagingPlayer >= opportunistMobThreshold;
            bool alerted = state == SoldierState.BurstFire || state == SoldierState.Alert;

            if (overwhelmed && !alerted && _currentAction != CombatAction.Opportunist)
            {
                SquadBlackboard.Instance.RegisterOpportunist(this);
                _opportunistEnterTime = Time.time;
                ReleaseRole();
                return CombatAction.Opportunist;
            }

            if (_currentAction == CombatAction.Opportunist)
            {
                bool killWindow = arena.PlayerHealthPct <= executeHpThreshold;
                bool mobsCleared = arena.MobsEngagingPlayer == 0;
                bool timedOut = Time.time - _opportunistEnterTime > opportunistTimeout;

                if (killWindow && SquadBlackboard.Instance.OpportunistCount >= 2)
                {
                    SquadBlackboard.Instance.TriggerAlphaStrike();
                }

                bool signalFired = SquadBlackboard.Instance.AlphaStrikeTimestamp > _lastAlphaStrikeSeen;
                if (signalFired)
                {
                    _lastAlphaStrikeSeen = SquadBlackboard.Instance.AlphaStrikeTimestamp;
                }

                if (signalFired || timedOut || mobsCleared)
                {
                    SquadBlackboard.Instance.UnregisterOpportunist(this);
                    ReleaseCover();
                    return CombatAction.Flank; // Engage and flank!
                }
                return CombatAction.Opportunist;
            }
        }
        // ------------------------------------

        if (_recentlyHit && _claimedCover == null)
        {
            var cp = G1CoverPoint.FindNearestValid(transform.position, player.transform.position, 15f);
            if (cp != null)
            {
                cp.Claimed = true;
                _claimedCover = cp;
                ReleaseRole();
                return CombatAction.MoveToCover;
            }
        }

        if (_claimedCover != null)
        {
            if (Vector3.Distance(transform.position, player.transform.position) < 5f)
            {
                ReleaseCover();
            }
            else
            {
                return CombatAction.PopFire;
            }
        }

        var flankRole = SquadBlackboard.Instance != null && SquadBlackboard.Instance.IsFree(SquadRole.FlankLeft) ? SquadRole.FlankLeft
                       : SquadBlackboard.Instance != null && SquadBlackboard.Instance.IsFree(SquadRole.FlankRight) ? SquadRole.FlankRight
                       : SquadRole.None;
        if (flankRole != SquadRole.None && SquadBlackboard.Instance != null && SquadBlackboard.Instance.TryClaim(flankRole, this))
        {
            _claimedRole = flankRole;
            return CombatAction.Flank;
        }

        if (SquadBlackboard.Instance != null && SquadBlackboard.Instance.TryClaim(SquadRole.Suppress, this))
        {
            _claimedRole = SquadRole.Suppress;
            return CombatAction.Suppress;
        }

        return CombatAction.PopFire;
    }

    private void ExecuteAction(CombatAction action)
    {
        ResetHeight();

        switch (action)
        {
            case CombatAction.Opportunist:
                if (_claimedCover == null)
                {
                    var cp = G1CoverPoint.FindNearestValid(transform.position, player.transform.position, 12f);
                    if (cp != null)
                    {
                        cp.Claimed = true;
                        _claimedCover = cp;
                    }
                }
                if (_claimedCover != null)
                {
                    agent.speed = patrolSpeed; // walk quietly, stay quiet
                    agent.SetDestination(_claimedCover.transform.position);

                    float distToCover = Vector3.Distance(transform.position, _claimedCover.transform.position);
                    if (distToCover < 0.8f)
                    {
                        Crouch();
                        agent.ResetPath();
                        if (anim && !anim.GetCurrentAnimatorStateInfo(0).IsName("Idle"))
                            anim.CrossFade("Idle", 0.1f);
                    }
                    else
                    {
                        if (anim && !anim.GetCurrentAnimatorStateInfo(0).IsName("Walk"))
                            anim.CrossFade("Walk", 0.1f);
                    }
                }
                else
                {
                    agent.ResetPath();
                    if (anim && !anim.GetCurrentAnimatorStateInfo(0).IsName("Idle"))
                        anim.CrossFade("Idle", 0.1f);
                }
                break;

            case CombatAction.Retreat:
                Vector3 retreatDir = (transform.position - player.transform.position).normalized;
                Vector3 retreatTarget = transform.position + retreatDir * 8f;
                agent.speed = chaseSpeed;
                agent.SetDestination(retreatTarget);
                if (anim && !anim.GetCurrentAnimatorStateInfo(0).IsName("Walk"))
                    anim.CrossFade("Walk", 0.1f);
                break;

            case CombatAction.Reload:
                agent.ResetPath();
                if (anim && !anim.GetCurrentAnimatorStateInfo(0).IsName("Idle"))
                    anim.CrossFade("Idle", 0.1f);
                break;

            case CombatAction.MoveToCover:
                if (_claimedCover != null)
                {
                    agent.speed = chaseSpeed;
                    agent.SetDestination(_claimedCover.transform.position);

                    float distToCover = Vector3.Distance(transform.position, _claimedCover.transform.position);
                    if (distToCover < 0.8f)
                    {
                        Crouch();
                        agent.ResetPath();
                        if (anim && !anim.GetCurrentAnimatorStateInfo(0).IsName("Idle"))
                            anim.CrossFade("Idle", 0.1f);
                    }
                    else
                    {
                        if (anim && !anim.GetCurrentAnimatorStateInfo(0).IsName("Walk"))
                            anim.CrossFade("Walk", 0.1f);
                    }
                }
                break;

            case CombatAction.PopFire:
            case CombatAction.Suppress:
                agent.ResetPath();
                FacePlayer();
                if (Time.time >= nextBurstTime && state != SoldierState.BurstFire)
                {
                    BeginBurst();
                }
                else if (state == SoldierState.BurstFire)
                {
                    TickBurstFire();
                }
                break;

            case CombatAction.Flank:
                SeekFlankPosition();
                float d = Vector3.Distance(transform.position, player.transform.position);
                if (d <= attackRadius && Time.time >= nextBurstTime && state != SoldierState.BurstFire)
                {
                    BeginBurst();
                }
                else if (state == SoldierState.BurstFire)
                {
                    TickBurstFire();
                }
                else
                {
                    if (anim && !anim.GetCurrentAnimatorStateInfo(0).IsName("Walk"))
                        anim.CrossFade("Walk", 0.1f);
                }
                break;
        }
    }

    private void Crouch()
    {
        agent.height = 1.0f;
        var col = GetComponent<CapsuleCollider>();
        if (col)
        {
            col.height = 1.0f;
            col.center = new Vector3(0f, 0.5f, 0f);
        }
    }

    private void ResetHeight()
    {
        agent.height = 1.8f;
        var col = GetComponent<CapsuleCollider>();
        if (col)
        {
            col.height = 1.8f;
            col.center = new Vector3(0f, 0.9f, 0f);
        }
    }

    private void FacePlayer()
    {
        Vector3 lookDir = player.transform.position - transform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }
    }

    void SeekFlankPosition()
    {
        if (player == null) return;

        Vector3 toPlayer = player.transform.position - transform.position;

        int activeCount = ThreatDirector.Instance != null ? ThreatDirector.Instance.ActiveSoldiersCount : 1;
        if (activeCount <= 1 || toPlayer.magnitude < 5f)
        {
            agent.SetDestination(player.transform.position);
            return;
        }

        Vector3 perp = Vector3.Cross(toPlayer.normalized, Vector3.up);
        int side = (gameObject.GetInstanceID() % 2 == 0) ? 1 : -1;
        Vector3 flankTarget = player.transform.position + perp * (side * 5f);

        if (UnityEngine.AI.NavMesh.SamplePosition(flankTarget, out UnityEngine.AI.NavMeshHit hit, 3f, UnityEngine.AI.NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
        else
        {
            agent.SetDestination(player.transform.position);
        }
    }

    void BeginBurst()
    {
        state = SoldierState.BurstFire;
        if (agent) agent.ResetPath();
        shotsLeftInBurst = 3;
        nextShotTime = Time.time;
        if (anim) anim.CrossFade("Idle", 0.05f);
    }

    void TickBurstFire()
    {
        FacePlayer();

        if (Time.time >= nextShotTime)
        {
            if (shotsLeftInBurst > 0)
            {
                ShootRound();
                shotsLeftInBurst--;
                nextShotTime = Time.time + burstGap;
            }
            else
            {
                state = SoldierState.Alert;
                nextBurstTime = Time.time + fireRate;
            }
        }
    }

    void ShootRound()
    {
        PlayMuzzleFlash();

        Vector3 eyePos = transform.position + Vector3.up * 1.5f + transform.forward * 0.45f;
        Vector3 targetPos = player.transform.position + Vector3.up * 1.5f;
        Vector3 dir = (targetPos - eyePos).normalized;

        dir = Quaternion.Euler(
            Random.Range(-2.5f, 2.5f),
            Random.Range(-2.5f, 2.5f),
            0f
        ) * dir;

        if (Physics.Raycast(eyePos, dir, out RaycastHit hit, detectRadius * 1.5f, obstacleMask))
        {
            if (hit.collider.gameObject == player)
            {
                playerHealth.TakeDamage(damage, hit.point, hit.normal);
                if (ThreatDirector.Instance != null)
                {
                    ThreatDirector.Instance.ReportPlayerHit();
                }
            }
        }
    }

    void PlayMuzzleFlash()
    {
        Transform hand = FindHand(transform);
        if (hand == null) hand = transform;

        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(flash.GetComponent<Collider>());
        flash.transform.position = hand.position + transform.forward * 0.18f;
        flash.transform.localScale = Vector3.one * 0.12f;

        var mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = new Color(1f, 0.75f, 0.15f);
        flash.GetComponent<MeshRenderer>().sharedMaterial = mat;

        Destroy(flash, 0.05f);
    }

    Transform FindHand(Transform parent)
    {
        if (parent.name == "hand.R") return parent;
        foreach (Transform child in parent)
        {
            var found = FindHand(child);
            if (found != null) return found;
        }
        return null;
    }

    void ResetPatrol()
    {
        state = SoldierState.Patrol;
        if (agent) agent.ResetPath();
        bool isWalking = waypoints != null && waypoints.Length > 1;
        if (anim) anim.CrossFade(isWalking ? "Walk" : "Idle", 0.1f);
    }

    private void ReleaseCover()
    {
        if (_claimedCover != null)
        {
            _claimedCover.Claimed = false;
            _claimedCover = null;
        }
    }

    private void ReleaseRole()
    {
        if (_claimedRole != SquadRole.None)
        {
            if (SquadBlackboard.Instance != null)
            {
                SquadBlackboard.Instance.Release(_claimedRole, this);
            }
            _claimedRole = SquadRole.None;
        }
    }

    void HandleDeath(Vector3 hitPoint, Vector3 hitNormal)
    {
        state = SoldierState.Dead;
        ReleaseCover();
        ReleaseRole();
        if (SquadBlackboard.Instance != null)
        {
            SquadBlackboard.Instance.UnregisterOpportunist(this);
        }
        if (agent) agent.enabled = false;
        if (ThreatDirector.Instance != null)
        {
            ThreatDirector.Instance.ReportSoldierDead();
        }
        TryDropPack();
    }

    void TryDropPack()
    {
        if (Random.value > dropChance) return;

        Vector3 dropPos = transform.position + Vector3.up * 0.12f;
        
        if (Random.value < 0.5f)
        {
            G1HealthPack.Create(dropPos);
        }
        else
        {
            G1AmmoPack.Create(dropPos);
        }
    }

    void OnDestroy()
    {
        if (myHealth)
            myHealth.OnDeath -= HandleDeath;
        ReleaseCover();
        ReleaseRole();
        if (SquadBlackboard.Instance != null)
        {
            SquadBlackboard.Instance.UnregisterOpportunist(this);
        }
    }
}
