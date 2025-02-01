using Fusion;
using UnityEngine;
using UnityEngine.AI;

public class EnemySpawner : NetworkBehaviour
{
    private TickTimer _tickDelay;
    private System.Random _random = new System.Random();
    private int _enemyCount = 0;

    [SerializeField] public int spawnRadius;
    [SerializeField] private BasicEnemy _basicEnemyPrefab;
    [SerializeField] private int _lowerLevel;
    [SerializeField] private int _upperLevel;
    [SerializeField] private int _maxEnemyCount;

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority && _tickDelay.ExpiredOrNotRunning(Runner) && _enemyCount < _maxEnemyCount)
        {
            var enemy = Runner.Spawn(_basicEnemyPrefab, GetPointInSpawnRadius(), Quaternion.identity, Object.InputAuthority);
            
            enemy.SetSpawner(this);
            enemy.CalculateMaxHealth(_random.Next(_lowerLevel, _upperLevel + 1));
            _enemyCount++;

            // By default spawn enemies at this rate
            _tickDelay = TickTimer.CreateFromSeconds(Runner, 5f);
        }
    }

    public override void Spawned()
    {
        _tickDelay = TickTimer.CreateFromSeconds(Runner, 5);
    }

    public Vector2 GetPointInSpawnRadius()
    {
        Vector2? testPoint = null;
        NavMeshHit hit = new NavMeshHit();
        int attempt = 0;

        while (attempt < 30 && (testPoint == null || !NavMesh.SamplePosition((Vector3)testPoint, out hit, 3f, NavMesh.AllAreas)))
        {
            testPoint = (Vector2)transform.position + (Random.insideUnitCircle * spawnRadius);
        }

        return hit.position;
    }

    public void OnEnemyDeath()
    {
        if (_enemyCount == _maxEnemyCount)
        {
            // If we are at max enemies, give time to let the player try to wipe out them all
            _tickDelay = TickTimer.CreateFromSeconds(Runner, 20);
        }

        _enemyCount--;
    }
}
