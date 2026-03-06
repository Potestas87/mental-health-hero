using UnityEngine;

public class DungeonSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject normalEnemyPrefab;
    public GameObject bossEnemyPrefab;

    [Header("Spawn Points")]
    public Transform[] floorSpawnPoints;
    public Transform bossSpawnPoint;

    [Header("References")]
    public HeroController2D player;
    public DungeonRunManager runManager;

    private GameObject _activeEnemy;

    public void SpawnFloorEnemy(int floorNumber)
    {
        if (normalEnemyPrefab == null)
        {
            Debug.LogWarning("DungeonSpawner: normalEnemyPrefab is not assigned.");
            return;
        }

        var spawnPoint = GetFloorSpawnPoint(floorNumber);
        if (spawnPoint == null)
        {
            Debug.LogWarning("DungeonSpawner: floor spawn point not configured.");
            return;
        }

        SpawnEnemy(normalEnemyPrefab, spawnPoint.position, false);
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

        SpawnEnemy(bossEnemyPrefab, bossSpawnPoint.position, true);
    }

    public void DespawnActiveEnemy()
    {
        if (_activeEnemy != null)
        {
            Destroy(_activeEnemy);
            _activeEnemy = null;
        }
    }

    private void SpawnEnemy(GameObject prefab, Vector3 position, bool makeBoss)
    {
        DespawnActiveEnemy();

        _activeEnemy = Instantiate(prefab, position, Quaternion.identity);
        var enemy = _activeEnemy.GetComponent<EnemyController2D>();
        if (enemy == null)
        {
            enemy = _activeEnemy.AddComponent<EnemyController2D>();
        }

        enemy.isBoss = makeBoss;

        if (player != null)
        {
            enemy.target = player.transform;
        }

        if (runManager != null)
        {
            enemy.runManager = runManager;
        }
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
