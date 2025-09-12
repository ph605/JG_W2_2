using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public enum MapType
{
    Line,
    Spread
};

public class MapGenerator : MonoBehaviour
{
    [Tooltip("현재 스테이지에 생성할 맵의 타입")]
    public MapType mapType;

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

    [Header("직선으로 생성하는 맵")]
    private float spawnZ = 0f;
    private List<GameObject> activeTiles = new List<GameObject>();
    // 타일 교체 시점을 관리할 변수
    private float tileRecycleTriggerZ;

    [Header("방사형으로 생성하는 맵")]
    // 생성된 타일의 그리드 좌표를 저장하여 중복 생성을 방지
    private HashSet<Vector2Int> spawnedTileCoords = new HashSet<Vector2Int>();
    // 활성화된 타일을 그리드 좌표와 함께 관리
    private Dictionary<Vector2Int, GameObject> activeSpreadTiles = new Dictionary<Vector2Int, GameObject>();
    // 각 좌표에 어떤 타일 패턴이 사용되었는지 영구적으로 기록하는 딕셔너리
    private Dictionary<Vector2Int, TilePattern> tileDataHistory = new Dictionary<Vector2Int, TilePattern>();

    // 플레이어의 이전 그리드 좌표를 저장
    private Vector2Int lastPlayerCoord;


    void Start()
    {
        DecideStartSpawnTile();

        GameObject prefabInPooler = ObjectPooler.Instance.pools[0].prefab;
        // MapGenerator 자신에게 등록된 프리팹
        GameObject prefabInGenerator = this.emptyTilePrefab;
    }

    void Update()
    {
        DecideUpdateSpawnTile();
    }

    // ================================================
    //              통합 타입의 맵 관리
    // ================================================

    // 시작할 때, 생성될 타일의 기준이 담겨 있는 함수
    private void DecideStartSpawnTile()
    {
        switch (mapType)
        {
            case MapType.Line:
                for (int i = 0; i < visibleTilesOnScreen; i++)
                {
                    // 플레이어가 첫 번째 타일의 절반을 지났을 때 다음 타일이 생성되도록 설정
                    tileRecycleTriggerZ = tileLength / 2;

                    SpawnTile();
                }
                break;

            case MapType.Spread:
                // 플레이어의 시작 그리드 좌표를 계산하고, 주변 타일을 즉시 생성
                lastPlayerCoord = GetPlayerTileLoc();
                SpawnSpreadTilesInRadius();
                break;
        }
    }

    // 다음에 생성될 타일의 기준이 담겨있는 함수
    private void DecideUpdateSpawnTile()
    {
        switch (mapType)
        {
            case MapType.Line:
                // 목표 지점이 생기기 전까지, 맵 생성
                if (isGoalSpawned)
                    return;

                float tileRecycleTriggerZ = spawnZ - (visibleTilesOnScreen * tileLength) + tileLength;
                if (playerTransform.position.z > tileRecycleTriggerZ)
                {
                    SpawnTile();
                    DeleteOldestTile();

                    // 다음 타일 교체 지점을 한 타일 길이만큼 앞으로 이동시킴
                    tileRecycleTriggerZ += tileLength;
                }
                break;

            case MapType.Spread:
                Vector2Int currentPlayerCoord = GetPlayerTileLoc();
                // 플레이어가 새로운 그리드 칸으로 이동했을 때만 실행
                if (currentPlayerCoord != lastPlayerCoord)
                {
                    lastPlayerCoord = currentPlayerCoord;
                    SpawnTile(); // 주변에 없는 타일 생성
                    DeleteOldestTile();    // 너무 멀어진 타일 제거
                }
                break;
        }
    }
    
    // 맵 생성
    private void SpawnTile()
    {
        switch(mapType)
        {
            case MapType.Line:
                SpawnLineTile();
                break;

            case MapType.Spread:
                SpawnSpreadTilesInRadius();
                break;
        }
    }

    // 맵 제거
    private void DeleteOldestTile()
    {
        switch (mapType)
        {
            case MapType.Line:
                DeleteOldestLineTile();
                break;

            case MapType.Spread:
                DeleteFarSpreadTiles();
                break;
        }
    }

    // ================================================
    //              직선형 타입의 맵 관리
    // ================================================

    // 직선형 맵 생성
    private void SpawnLineTile()
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

    // 직선형 맵 제거
    private void DeleteOldestLineTile()
    {
        // 1. 가장 오래된 타일을 파괴하는 대신 비활성화하여 풀에 돌려보내는 효과
        GameObject tileToReturn = activeTiles[0];
        // 2. TileBehavior에 있는 장애물들도 모두 풀에 반납하도록 명령 (아래 TileBehavior 수정 참고)
        tileToReturn.GetComponent<TileBehavior>().ReturnAllObstaclesToPool();

        ObjectPooler.Instance.ReturnToPool(tileToReturn);

        // 3. 목록에서만 제거
        activeTiles.RemoveAt(0);
    }

    // ================================================
    //              방사형 타입의 맵 관리
    // ================================================

    // 플레이어의 현재 월드 위치를 타일 좌표로 변환
    private Vector2Int GetPlayerTileLoc()
    {
        int x = Mathf.RoundToInt(playerTransform.position.x / tileLength);
        int z = Mathf.RoundToInt(playerTransform.position.z / tileLength);
        return new Vector2Int(x, z);
    }

    // 플레이어 주변의 정의된 반경 내에 타일을 생성
    private void SpawnSpreadTilesInRadius()
    {
        for (int x = -visibleTilesOnScreen; x <= visibleTilesOnScreen; x++)
        {
            for (int z = -visibleTilesOnScreen; z <= visibleTilesOnScreen; z++)
            {
                Vector2Int tileLoc = new Vector2Int(lastPlayerCoord.x + x, lastPlayerCoord.y + z);

                // 이 좌표에 타일이 아직 생성되지 않았다면
                if (!spawnedTileCoords.Contains(tileLoc))
                {
                    SpawnSingleSpreadTile(tileLoc);
                }
            }
        }
    }

    // 지정된 그리드 좌표에 타일 하나를 생성
    private void SpawnSingleSpreadTile(Vector2Int tileLoc)
    {
        Vector3 position = new Vector3(tileLoc.x * tileLength, 0, tileLoc.y * tileLength);
        GameObject newTile = ObjectPooler.Instance.SpawnFromPool(emptyTilePrefab, position, Quaternion.identity);

        spawnedTileCoords.Add(tileLoc);
        activeSpreadTiles.Add(tileLoc, newTile);

        if (tilePatterns.Count == 0) 
            return;

        // 기존에 사용한 패턴이 있으면, 그대로 사용
        TilePattern selectedPattern;
        if (tileDataHistory.ContainsKey(tileLoc))
        {
            selectedPattern = tileDataHistory[tileLoc];
        }
        else
        {
            selectedPattern = tilePatterns[Random.Range(0, tilePatterns.Count)];
            tileDataHistory.Add(tileLoc, selectedPattern);
        }
        
        newTile.GetComponent<TileBehavior>().GenerateObstacles(selectedPattern);
    }

    // 플레이어로부터 너무 멀리 떨어진 타일을 제거
    private void DeleteFarSpreadTiles()
    {
        // Dictionary의 모든 키(좌표)를 복사하여 순회 (순회 중 삭제 에러 방지)
        foreach (Vector2Int tileCoord in activeSpreadTiles.Keys.ToList())
        {
            // 타일과 플레이어 사이의 그리드 거리를 계산
            float distance = Vector2Int.Distance(tileCoord, lastPlayerCoord);

            int diffX = Mathf.Abs(tileCoord.x - lastPlayerCoord.x);
            int diffZ = Mathf.Abs(tileCoord.y - lastPlayerCoord.y);

            // 거리가 시야 반경 + 여유분보다 크면
            if (diffX > visibleTilesOnScreen || diffZ > visibleTilesOnScreen)
            {
                GameObject tileToReturn = activeSpreadTiles[tileCoord];
                tileToReturn.GetComponent<TileBehavior>().ReturnAllObstaclesToPool();
                ObjectPooler.Instance.ReturnToPool(tileToReturn);

                activeSpreadTiles.Remove(tileCoord);
                spawnedTileCoords.Remove(tileCoord); // 다시 생성될 수 있도록 HashSet에서도 제거
            }
        }
    }
}
