using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable] // Inspector 창에 보이게 함
public struct WeightedObstacleType
{
    public ObstacleType type;
    [Tooltip("이 장애물이 선택될 확률 가중치. 값이 높을수록 더 자주 등장합니다.")]
    [Range(1, 100)]
    public int weight;
    public bool isRandomScale;
    public Vector3 fixedScale;

    public Vector2 xBounds;
    public Vector2 yBounds;
    public Vector2 zBounds;

    public bool isActive;
}

public class TilePatternGenerator : MonoBehaviour
{
    [Header("생성 설정")]
    [Tooltip("생성할 패턴 파일의 이름")]
    public string fileName = "RandomPattern_01";

    [Tooltip("패턴 파일을 저장할 Assets 폴더 하위 경로")]
    public string savePath = "TilePatterns/Random";

    [Header("장애물 무작위 생성 규칙")]
    [Tooltip("하나의 타일 패턴에 생성할 장애물의 개수")]
    [Range(1, 20)]
    public int obstacleCount = 10;

    [Tooltip("각 장애물 타입의 생성 확률 가중치 목록입니다.")]
    public List<WeightedObstacleType> weightedObstacleTypes;

    [Tooltip("장애물과 장애물 사이의 최소 간격")]
    public float minDistanceBetweenObstacles = 3f;

    [Tooltip("겹치지 않는 위치를 찾기 위한 최대 시도 횟수 (무한 루프 방지)")]
    public int maxPlacementTries = 50;

    [Tooltip("장애물의 Y축 회전값을 무작위로 설정할지 여부")]
    public bool randomizeYRotation = true;

    [Tooltip("장애물의 크기를 무작위로 설정할 범위 (최소, 최대)")]
    public Vector2 randomScaleRange = new Vector2(0.8f, 2.0f);

    // Inspector 창에서 이 함수를 버튼처럼 실행할 수 있게 해주는 속성
    [ContextMenu("Generate New Random Pattern")]
    private void GeneratePattern()
    {
        // 1. 새로운 TilePattern 인스턴스(데이터 파일)를 메모리에 생성
        TilePattern newPattern = ScriptableObject.CreateInstance<TilePattern>();

        // 2. 장애물 데이터를 담을 리스트 생성
        List<ObstacleData> obstacleLayout = new List<ObstacleData>();

        // 3. 정해진 개수만큼 장애물 데이터 무작위 생성
        for (int i = 0; i < obstacleCount; i++)
        {
            // 무작위로 장애물 타입 선택
            WeightedObstacleType randomObstacleData = GetRandomObstacleTypeByWeight();
            bool positionFound = false;
            Vector3 randomPosition = Vector3.zero;

            // 겹치지 않는 유효한 위치 찾기
            for (int tries = 0; tries < maxPlacementTries; tries++)
            {
                float randomX = Random.Range(randomObstacleData.xBounds.x, randomObstacleData.xBounds.y);
                float randomY = Random.Range(randomObstacleData.yBounds.x, randomObstacleData.yBounds.y);
                float randomZ = Random.Range(randomObstacleData.zBounds.x, randomObstacleData.zBounds.y);
                randomPosition = new Vector3(randomX, randomY, randomZ);

                // 해당 위치가 유효한지 확인
                bool isOverlapping = false;
                foreach (var placedObstacle in obstacleLayout)
                {
                    // 이미 배치된 장애물과의 거리를 계산
                    if (Vector3.Distance(placedObstacle.position, randomPosition) < minDistanceBetweenObstacles)
                    {
                        isOverlapping = true;
                        break;
                    }
                }

                // 겹치지 않았다면, 위치를 찾았으므로 시도 중단
                if (isOverlapping == false)
                {
                    positionFound = true;
                    break;
                }
            }

            // 유효한 위치를 찾았다면, 장애물 데이터 생성
            if (positionFound)
            {
                // 무작위 회전값 생성
                Vector3 randomRotation = Vector3.zero;
                if (randomizeYRotation)
                    randomRotation.y = Random.Range(0, 360);

                Vector3 finalScale;
                if (randomObstacleData.isRandomScale)
                {
                    // 무작위 크기 생성
                    float scaleValue = Random.Range(randomScaleRange.x, randomScaleRange.y);
                    finalScale = new Vector3(scaleValue, scaleValue, scaleValue);
                }
                else
                {
                    finalScale = randomObstacleData.fixedScale;
                }

                // 생성된 무작위 데이터로 ObstacleData 구조체 채우기
                obstacleLayout.Add(new ObstacleData(0, randomObstacleData.type, randomPosition, randomRotation, finalScale, randomObstacleData.isActive));
            }
            // 유효한 위치를 찾지 못했을 때, 경고 메시지 출력
            else
            {
                Debug.LogWarning("장애물을 배치할 공간을 찾지 못했습니다. 장애물 개수나 최소 거리를 조절해 주세요.");
            }

        }

        // 4. 생성된 장애물 데이터 리스트를 TilePattern에 할당
        newPattern.obstacleLayout = obstacleLayout.ToArray();

        // 5. 생성된 TilePattern을 실제 .asset 파일로 저장
#if UNITY_EDITOR
        string fullPath = "Assets/" + savePath;
        if (!AssetDatabase.IsValidFolder(fullPath))
        {
            // 폴더가 없으면 새로 생성
            string[] folders = savePath.Split('/');
            string currentPath = "Assets";
            foreach (string folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(currentPath + "/" + folder))
                {
                    AssetDatabase.CreateFolder(currentPath, folder);
                }
                currentPath += "/" + folder;
            }
        }

        // 파일 저장
        AssetDatabase.CreateAsset(newPattern, fullPath + "/" + fileName + ".asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("새로운 타일 패턴 생성 완료: " + fullPath + "/" + fileName + ".asset");
#endif
    }

    // 가중치 기반 무작위 선택 함수
    private WeightedObstacleType GetRandomObstacleTypeByWeight()
    {
        if (weightedObstacleTypes == null || weightedObstacleTypes.Count == 0)
        {
            Debug.LogError("가중치 목록(weightedObstacleTypes)이 비어있습니다!");
            return new WeightedObstacleType() { type = 0, weight = 1, isRandomScale = true, fixedScale = Vector3.one };
        }

        // 1. 모든 가중치의 합을 계산
        int totalWeight = 0;
        foreach (var weightedType in weightedObstacleTypes)
        {
            totalWeight += weightedType.weight;
        }

        // 2. 0부터 (전체 가중치 합 - 1) 사이의 무작위 숫자를 뽑음
        int randomWeightValue = Random.Range(0, totalWeight);

        // 3. 무작위 숫자가 어떤 가중치 구간에 속하는지 찾아냄
        foreach (var weightedType in weightedObstacleTypes)
        {
            // 현재 타입의 가중치를 무작위 숫자에서 뺌
            randomWeightValue -= weightedType.weight;

            // 뺐을 때 0보다 작아지면, 현재 타입으로 결정
            if (randomWeightValue < 0)
                return weightedType;
        }

        // 만약을 대비한 기본값 반환
        return weightedObstacleTypes.Last();
    }
}
