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

    private GameObject _activeEnemy;

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
        SpawnEnemy(prefabToSpawn, spawnPoint.position, false, archetype, overrideArchetype);
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

        SpawnEnemy(bossEnemyPrefab, bossSpawnPoint.position, true, EnemyArchetype.Bruiser, true);
    }

    public void DespawnActiveEnemy()
    {
        if (_activeEnemy != null)
        {
            Destroy(_activeEnemy);
            _activeEnemy = null;
        }
    }

    private void SpawnEnemy(GameObject prefab, Vector3 position, bool makeBoss, EnemyArchetype archetype, bool overrideArchetype)
    {
        DespawnActiveEnemy();

        _activeEnemy = Instantiate(prefab, position, Quaternion.identity);
        var enemy = _activeEnemy.GetComponent<EnemyController2D>();
        if (enemy == null)
        {
            enemy = _activeEnemy.AddComponent<EnemyController2D>();
        }

        enemy.isBoss = makeBoss;
        if (overrideArchetype)
        {
            enemy.archetype = archetype;
        }

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
}
