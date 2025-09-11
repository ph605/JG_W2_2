using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    [Header("오브젝트 연결")]
    public Transform playerTransform;
    [Tooltip("장애물이 없는 비어있는 기본 타일 프리팹")]
    public GameObject emptyTilePrefab; // 기존 tilePrefabs 대신 사용

    [Header("맵 설정")]
    [Tooltip("사용할 맵 타일 패턴(ScriptableObject) 목록")]
    public List<TilePattern> tilePatterns; // GameObject 목록 대신 TilePattern 목록

    public float tileLength = 50f;
    public int visibleTilesOnScreen = 5;

    [Header("결승 지점 설정")]
    [Tooltip("결승 지점까지 생성할 일반 타일의 총 개수")]
    public int totalTilesToGoal = 30;
    [Tooltip("결승 지점 역할을 할 타일 프리팹")]
    public GameObject finishLinePrefab;

    private int tilesSpawnedCount = 1;
    private bool isGoalSpawned = false;

    private float spawnZ = 0f;
    private List<GameObject> activeTiles = new List<GameObject>();

    // 타일 교체 시점을 관리할 변수
    private float tileRecycleTriggerZ;

    void Start()
    {
        spawnZ = playerTransform.position.z;
        for (int i = 0; i < visibleTilesOnScreen; i++)
        {
            SpawnTile();
        }

        // 플레이어가 첫 번째 타일의 절반을 지났을 때 다음 타일이 생성되도록 설정
        tileRecycleTriggerZ = tileLength / 2;

        GameObject prefabInPooler = ObjectPooler.Instance.pools[0].prefab;
        // MapGenerator 자신에게 등록된 프리팹
        GameObject prefabInGenerator = this.emptyTilePrefab;
    }

    void Update()
    {
        // 목표 지점이 생기기 전까지, 맵 생성
        if (isGoalSpawned)
            return;

        float tileRecycleTriggerZ = spawnZ - (visibleTilesOnScreen * tileLength) + tileLength;
        if (playerTransform.position.z > tileRecycleTriggerZ)
        {
            Debug.Log("Spawn");
            SpawnTile();
            DeleteOldestTile();

            // 다음 타일 교체 지점을 한 타일 길이만큼 앞으로 이동시킴
            tileRecycleTriggerZ += tileLength;
        }
    }
    private void SpawnTile()
    {
        // 결승 지점 맵 생성
        if (tilesSpawnedCount >= totalTilesToGoal)
        {
            isGoalSpawned = true;
            GameObject finishTile = Instantiate(finishLinePrefab, Vector3.forward * spawnZ, Quaternion.identity);
            activeTiles.Add(finishTile);
            spawnZ += tileLength;

            return;
        }

        // 1. 비어있는 기본 타일을 풀에서 가져옴
        GameObject newTile = ObjectPooler.Instance.SpawnFromPool(emptyTilePrefab, Vector3.forward * spawnZ, Quaternion.identity);
        activeTiles.Add(newTile);
        spawnZ += tileLength;

        // 현재 패턴이 없으면, 
        if (tilePatterns.Count == 0)
            return;

        TilePattern selectedPattern;
        // 2. 순서대로 모든 패턴 선택
        if (tilesSpawnedCount < tilePatterns.Count)
        {
            // 리스트의 순서에 따라 패턴을 선택
            selectedPattern = tilePatterns[tilesSpawnedCount];
        }
        // 나중에는 무작위 패턴 선택
        else
        {
            selectedPattern = tilePatterns[Random.Range(0, tilePatterns.Count)];
        }
        ++tilesSpawnedCount;
        // 3. 타일에 붙어있는 TileBehavior 스크립트에게 선택된 패턴으로 장애물을 생성하라고 명령
        newTile.GetComponent<TileBehavior>().GenerateObstacles(selectedPattern);
    }

    private void DeleteOldestTile()
    {
        // 1. 가장 오래된 타일을 파괴하는 대신 비활성화하여 풀에 돌려보내는 효과
        GameObject tileToReturn  = activeTiles[0];
        // 2. TileBehavior에 있는 장애물들도 모두 풀에 반납하도록 명령 (아래 TileBehavior 수정 참고)
        tileToReturn.GetComponent<TileBehavior>().ReturnAllObstaclesToPool();

        ObjectPooler.Instance.ReturnToPool(tileToReturn);

        // 3. 목록에서만 제거
        activeTiles.RemoveAt(0);
    }
}
