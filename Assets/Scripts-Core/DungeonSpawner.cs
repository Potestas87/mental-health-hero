using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DungeonSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject normalEnemyPrefab;
    public GameObject[] floorEnemyPrefabs;
    public GameObject bossEnemyPrefab;

    [Header("Spawn Points")]
    public Transform[] floorSpawnPoints;
    public Transform bossSpawnPoint;

    [Header("References")]
    public HeroController2D player;
    public DungeonRunManager runManager;

    [Header("Enemy Variety")]
    public bool randomizeFloorArchetypes = true;
    public EnemyArchetype[] floorArchetypePool =
    {
        EnemyArchetype.Chaser,
        EnemyArchetype.Tank,
        EnemyArchetype.Skirmisher,
        EnemyArchetype.Bruiser,
        EnemyArchetype.RangedProxy
    };

    [Header("Wave Spawning")]
    public bool useRandomWaveSpawns = true;
    public Vector2 waveSpawnMin = new Vector2(-18f, -18f);
    public Vector2 waveSpawnMax = new Vector2(18f, 18f);
    public int maxSpawnPositionAttempts = 16;
    public float spawnCollisionRadius = 0.55f;
    public float minimumDistanceFromPlayer = 2.5f;
    public LayerMask blockedSpawnLayers;

    private GameObject _activeEnemy;
    private readonly List<GameObject> _spawnedEnemies = new List<GameObject>();
    private Coroutine _waveRoutine;
    private bool _bossQueuedAfterWaves;
    private bool _bossSpawnedAfterWaves;

    public void SpawnFloorEnemy(int floorNumber)
    {
        var prefabToSpawn = PickFloorEnemyPrefab();
        if (prefabToSpawn == null)
        {
            Debug.LogWarning("DungeonSpawner: no floor enemy prefab is assigned.");
            return;
        }

        var spawnPoint = GetFloorSpawnPoint(floorNumber);
        if (spawnPoint == null)
        {
            Debug.LogWarning("DungeonSpawner: floor spawn point not configured.");
            return;
        }

        var overrideArchetype = floorEnemyPrefabs == null || floorEnemyPrefabs.Length == 0;
        var archetype = PickFloorArchetype(floorNumber);
        SpawnEnemy(prefabToSpawn, spawnPoint.position, false, archetype, overrideArchetype, true, true);
    }

    public void SpawnBoss()
    {
        if (bossEnemyPrefab == null)
        {
            Debug.LogWarning("DungeonSpawner: bossEnemyPrefab is not assigned.");
            return;
        }

        if (bossSpawnPoint == null)
        {
            Debug.LogWarning("DungeonSpawner: bossSpawnPoint is not assigned.");
            return;
        }

        SpawnEnemy(bossEnemyPrefab, bossSpawnPoint.position, true, EnemyArchetype.Bruiser, true, true, true);
    }

    public void DespawnActiveEnemy()
    {
        if (_activeEnemy != null)
        {
            Destroy(_activeEnemy);
            _activeEnemy = null;
        }
    }

    public void DespawnAllSpawnedEnemies()
    {
        DespawnActiveEnemy();
        for (var i = _spawnedEnemies.Count - 1; i >= 0; i--)
        {
            var enemy = _spawnedEnemies[i];
            if (enemy != null)
            {
                Destroy(enemy);
            }
        }

        _spawnedEnemies.Clear();
        _bossQueuedAfterWaves = false;
        _bossSpawnedAfterWaves = false;
    }

    public void StartWaveSpawning(int waves, int enemiesPerWave, float secondsBetweenWaves)
    {
        StopWaveSpawning();
        _bossQueuedAfterWaves = false;
        _bossSpawnedAfterWaves = false;
        _waveRoutine = StartCoroutine(WaveRoutine(
            Mathf.Max(1, waves),
            Mathf.Max(1, enemiesPerWave),
            Mathf.Max(0.1f, secondsBetweenWaves)));
    }

    public void StopWaveSpawning()
    {
        if (_waveRoutine != null)
        {
            StopCoroutine(_waveRoutine);
            _waveRoutine = null;
        }
    }

    public void NotifySpawnedEnemyDefeated(EnemyController2D enemy)
    {
        if (enemy == null)
        {
            return;
        }

        _spawnedEnemies.Remove(enemy.gameObject);
        TrySpawnBossAfterWaves();
    }

    private IEnumerator WaveRoutine(int waves, int enemiesPerWave, float secondsBetweenWaves)
    {
        for (var waveIndex = 0; waveIndex < waves; waveIndex++)
        {
            for (var i = 0; i < enemiesPerWave; i++)
            {
                SpawnWaveEnemy();
            }

            if (runManager != null)
            {
                runManager.SetExternalStatus("Wave " + (waveIndex + 1) + "/" + waves + " spawned (" + enemiesPerWave + " enemies).");
            }

            if (waveIndex < waves - 1)
            {
                yield return new WaitForSeconds(secondsBetweenWaves);
            }
        }

        _bossQueuedAfterWaves = true;
        TrySpawnBossAfterWaves();
        _waveRoutine = null;
    }

    private void TrySpawnBossAfterWaves()
    {
        if (!_bossQueuedAfterWaves || _bossSpawnedAfterWaves)
        {
            return;
        }

        if (_spawnedEnemies.Count > 0)
        {
            return;
        }

        _bossSpawnedAfterWaves = true;
        SpawnBoss();
        if (runManager != null)
        {
            runManager.SetExternalStatus("All waves cleared. Boss spawned.");
        }
    }

    private void SpawnWaveEnemy()
    {
        var prefabToSpawn = PickFloorEnemyPrefab();
        if (prefabToSpawn == null)
        {
            return;
        }

        var spawnPosition = TryGetWaveSpawnPosition(out var pos) ? pos : Vector3.zero;
        var overrideArchetype = floorEnemyPrefabs == null || floorEnemyPrefabs.Length == 0;
        var archetype = PickFloorArchetype(1);
        SpawnEnemy(prefabToSpawn, spawnPosition, false, archetype, overrideArchetype, false, false);
    }

    private bool TryGetWaveSpawnPosition(out Vector3 position)
    {
        if (!useRandomWaveSpawns)
        {
            var randomSpawn = GetRandomConfiguredSpawnPoint();
            if (randomSpawn != null)
            {
                position = randomSpawn.position;
                return true;
            }
        }

        var mask = blockedSpawnLayers.value == 0 ? Physics2D.DefaultRaycastLayers : blockedSpawnLayers.value;
        for (var attempt = 0; attempt < Mathf.Max(1, maxSpawnPositionAttempts); attempt++)
        {
            var x = Random.Range(waveSpawnMin.x, waveSpawnMax.x);
            var y = Random.Range(waveSpawnMin.y, waveSpawnMax.y);
            var candidate = new Vector2(x, y);

            if (player != null)
            {
                var distanceToPlayer = Vector2.Distance(candidate, player.transform.position);
                if (distanceToPlayer < minimumDistanceFromPlayer)
                {
                    continue;
                }
            }

            var blocked = Physics2D.OverlapCircle(candidate, spawnCollisionRadius, mask);
            if (blocked != null)
            {
                continue;
            }

            position = new Vector3(candidate.x, candidate.y, 0f);
            return true;
        }

        // Fallback to first configured floor spawn if no open random point found.
        var fallback = GetFloorSpawnPoint(1);
        if (fallback != null)
        {
            position = fallback.position;
            return true;
        }

        position = Vector3.zero;
        return false;
    }

    private Transform GetRandomConfiguredSpawnPoint()
    {
        if (floorSpawnPoints == null || floorSpawnPoints.Length == 0)
        {
            return null;
        }

        var valid = new List<Transform>();
        for (var i = 0; i < floorSpawnPoints.Length; i++)
        {
            if (floorSpawnPoints[i] != null)
            {
                valid.Add(floorSpawnPoints[i]);
            }
        }

        if (valid.Count == 0)
        {
            return null;
        }

        var idx = Random.Range(0, valid.Count);
        return valid[idx];
    }

    private void SpawnEnemy(
        GameObject prefab,
        Vector3 position,
        bool makeBoss,
        EnemyArchetype archetype,
        bool overrideArchetype,
        bool replaceExisting,
        bool reportDefeatToRunManager)
    {
        if (replaceExisting)
        {
            DespawnActiveEnemy();
        }

        var spawned = Instantiate(prefab, position, Quaternion.identity);
        if (replaceExisting)
        {
            _activeEnemy = spawned;
        }
        else
        {
            _spawnedEnemies.Add(spawned);
        }

        var enemyRenderer = spawned.GetComponent<SpriteRenderer>();
        if (enemyRenderer != null)
        {
            // Keep enemies visible above floor sprites while still below the player.
            enemyRenderer.sortingOrder = 1;
        }

        var enemy = spawned.GetComponent<EnemyController2D>();
        if (enemy == null)
        {
            enemy = spawned.AddComponent<EnemyController2D>();
        }

        enemy.isBoss = makeBoss;
        if (overrideArchetype)
        {
            enemy.archetype = archetype;
        }
        enemy.reportDefeatToRunManager = reportDefeatToRunManager;
        enemy.spawner = this;

        if (player != null)
        {
            enemy.target = player.transform;
        }

        if (runManager != null)
        {
            enemy.runManager = runManager;
        }
    }

    private EnemyArchetype PickFloorArchetype(int floorNumber)
    {
        if (!randomizeFloorArchetypes || floorArchetypePool == null || floorArchetypePool.Length == 0)
        {
            return EnemyArchetype.Chaser;
        }

        // Slightly bias to tougher archetypes later in run.
        if (floorNumber >= 3)
        {
            var weightedRoll = Random.Range(0, 100);
            if (weightedRoll < 35) return EnemyArchetype.Bruiser;
            if (weightedRoll < 60) return EnemyArchetype.Tank;
            if (weightedRoll < 80) return EnemyArchetype.RangedProxy;
        }

        var index = Random.Range(0, floorArchetypePool.Length);
        return floorArchetypePool[index];
    }

    private GameObject PickFloorEnemyPrefab()
    {
        if (floorEnemyPrefabs != null && floorEnemyPrefabs.Length > 0)
        {
            var validCount = 0;
            for (var i = 0; i < floorEnemyPrefabs.Length; i++)
            {
                if (floorEnemyPrefabs[i] != null)
                {
                    validCount++;
                }
            }

            if (validCount > 0)
            {
                var pick = Random.Range(0, validCount);
                for (var i = 0; i < floorEnemyPrefabs.Length; i++)
                {
                    if (floorEnemyPrefabs[i] == null)
                    {
                        continue;
                    }

                    if (pick == 0)
                    {
                        return floorEnemyPrefabs[i];
                    }

                    pick--;
                }
            }
        }

        return normalEnemyPrefab;
    }

    private Transform GetFloorSpawnPoint(int floorNumber)
    {
        if (floorSpawnPoints == null || floorSpawnPoints.Length == 0)
        {
            return null;
        }

        var index = Mathf.Clamp(floorNumber - 1, 0, floorSpawnPoints.Length - 1);
        return floorSpawnPoints[index];
    }

    private void OnDrawGizmosSelected()
    {
        if (!useRandomWaveSpawns)
        {
            return;
        }

        Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.45f);
        var center = new Vector3((waveSpawnMin.x + waveSpawnMax.x) * 0.5f, (waveSpawnMin.y + waveSpawnMax.y) * 0.5f, 0f);
        var size = new Vector3(Mathf.Abs(waveSpawnMax.x - waveSpawnMin.x), Mathf.Abs(waveSpawnMax.y - waveSpawnMin.y), 0f);
        Gizmos.DrawWireCube(center, size);
    }
}
