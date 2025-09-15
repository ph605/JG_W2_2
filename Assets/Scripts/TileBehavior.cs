using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Linq;

public class TileBehavior : MonoBehaviour
{
    // 어떤 장애물 타입이 어떤 프리팹에 해당하는지 연결해주는 리스트
    [System.Serializable]
    public struct ObstaclePrefabMapping
    {
        public ObstacleType type;
        public GameObject prefab;
    }

    public List<ObstaclePrefabMapping> obstaclePrefabs;

    [Header("테스트 모드")]
    public GameObject Floor;
    public TextMeshProUGUI patternNameText;

    // ObstacleType을 키로 사용하여 빠르게 프리팹을 찾기 위한 딕셔너리
    private Dictionary<ObstacleType, GameObject> obstaclePrefabDict;

    void Awake()
    {
        // 리스트를 딕셔너리로 변환하여 검색 속도를 높임
        obstaclePrefabDict = new Dictionary<ObstacleType, GameObject>();
        foreach (var mapping in obstaclePrefabs)
        {
            obstaclePrefabDict[mapping.type] = mapping.prefab;
        }
    }

    // MapGenerator가 호출할 함수. 패턴 데이터를 받아 장애물을 생성
    public void GenerateObstacles(TilePattern pattern, bool isTestMode)
    {
        for(int i = 0; i < pattern.obstacleLayout.Count(); i++)
        {
            ObstacleData obstacleData = pattern.obstacleLayout[i];

            if (obstaclePrefabDict.TryGetValue(obstacleData.type, out GameObject prefab))
            {
                // Instantiate 대신 ObjectPooler 사용 (태그는 프리팹 이름 등으로 미리 약속)
                Quaternion rotation = Quaternion.Euler(obstacleData.eulerAngles);
                GameObject obstacle = ObjectPooler.Instance.SpawnFromPool(prefab, transform.position + obstacleData.position, rotation);

                // 비활성화여도 풀에서 생성은 하기
                obstacle.gameObject.SetActive(obstacleData.isActive);

                if (isTestMode)
                {
                    patternNameText.text = pattern.name;
                    patternNameText.gameObject.SetActive(true);
                    Floor.gameObject.SetActive(true);
                }

                if (obstacle != null)
                {
                    PooledObjectInfo info = obstacle.GetComponent<PooledObjectInfo>();
                    if (info != null)
                    {
                        // 설계도(ObstacleData)에 저장된 영구 ID를
                        // 실제 객체(GameObject)의 정보 컴포넌트에 복사합니다.
                        info.dataID = obstacleData.id;
                    }

                    obstacle.transform.SetParent(transform);

                    Vector3 finalScale = obstacleData.scale;
                    if (finalScale == Vector3.zero)
                        finalScale = Vector3.one;

                    obstacle.transform.localScale = finalScale;

                    // 상호작용 가능한 아이템인지 확인
                    ItemInteraction item = obstacle.GetComponent<ItemInteraction>();
                    if(item != null)
                    {
                        item.SetStartPos(transform.position + obstacleData.position);
                    }
                }
            }
        }
    }

    // 타일이 재사용되기 전에 자식 장애물들을 모두 풀에 반납하는 함수
    public void ReturnAllObstaclesToPool()
    {
        // 자식 오브젝트의 수만큼 반복 (역순으로 해야 안전)
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;

            // 1. 자식 오브젝트에서 PooledObjectInfo 컴포넌트를 찾아봄
            PooledObjectInfo pooledObj = child.GetComponent<PooledObjectInfo>();

            // 2. 컴포넌트가 존재할 때만 (즉, 풀링으로 생성된 장애물일 때만) 반납 로직을 실행
            if (pooledObj == null)
                continue;

            // 자식 관계를 먼저 해제
            child.transform.SetParent(null);

            // 태그 없이 오브젝트 자체를 넘겨주어 반납
            ObjectPooler.Instance.ReturnToPool(child);
        }
    }
}