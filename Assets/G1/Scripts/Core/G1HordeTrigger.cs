using System.Collections;
using UnityEngine;

public class G1HordeTrigger : MonoBehaviour
{
    public int spawnCount = 8;
    public Vector3 spawnCenter;
    public float spawnRadius = 4f;
    public float spawnInterval = 0.15f;

    bool _fired;  // one-shot guard

    private void OnTriggerEnter(Collider other)
    {
        if (_fired) return;
        if (!other.CompareTag("Player")) return;
        _fired = true;
        StartCoroutine(SpawnBurst());
    }

    private IEnumerator SpawnBurst()
    {
        var director = ThreatDirector.Instance;
        if (director == null) yield break;

        for (int i = 0; i < spawnCount; i++)
        {
            // Alternate zombie/alien 50-50
            Vector2 rand = Random.insideUnitCircle * spawnRadius;
            Vector3 pos = spawnCenter + new Vector3(rand.x, 0f, rand.y);
            director.ForceSpawnHorde(pos, i % 2 == 0);
            yield return new WaitForSeconds(spawnInterval);
        }
    }
}
