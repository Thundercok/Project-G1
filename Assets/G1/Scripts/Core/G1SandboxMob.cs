using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// Minimal NavMesh-based mob brain for sandbox testing.
/// No Animator required — works on a plain capsule primitive.
/// Chases the player, attacks when close, and dies properly.
[RequireComponent(typeof(NavMeshAgent), typeof(HealthSystem))]
public class G1SandboxMob : MonoBehaviour
{
    public float attackRange    = 1.8f;
    public float attackInterval = 1.4f;
    public float damage         = 15f;

    NavMeshAgent _agent;
    HealthSystem _health;
    GameObject   _player;
    HealthSystem _playerHealth;
    float        _nextAttack;
    Renderer     _rend;
    Color        _baseColor;

    void Start()
    {
        _agent        = GetComponent<NavMeshAgent>();
        _health       = GetComponent<HealthSystem>();
        _rend         = GetComponent<Renderer>();
        _baseColor    = _rend ? _rend.material.color : Color.white;
        _player       = GameObject.FindWithTag("Player");
        if (_player)  _playerHealth = _player.GetComponent<HealthSystem>();

        _health.OnDeath += (p, n) =>
        {
            _agent.enabled = false;
            if (_rend) _rend.material.color = new Color(0.2f, 0.2f, 0.2f);
            Destroy(gameObject, 2.5f);
        };
    }

    void Update()
    {
        if (_health.IsDead || _player == null) return;
        if (_playerHealth != null && _playerHealth.IsDead) return;
        if (!_agent.isOnNavMesh) return;

        // Chase
        _agent.SetDestination(_player.transform.position);

        // Face player
        Vector3 dir = (_player.transform.position - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(
                transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);

        // Attack
        float dist = Vector3.Distance(transform.position, _player.transform.position);
        if (dist <= attackRange && Time.time >= _nextAttack)
        {
            _nextAttack = Time.time + attackInterval;
            _playerHealth?.TakeDamage(damage, transform.position, dir.normalized);
            // Flash red on attack
            if (_rend) StartCoroutine(FlashRed());
            G1Audio.Play("enemy_attack", transform.position, 0.65f);
        }
    }

    System.Collections.IEnumerator FlashRed()
    {
        if (_rend) _rend.material.color = Color.red;
        yield return new WaitForSeconds(0.12f);
        if (_rend) _rend.material.color = _baseColor;
    }
}
