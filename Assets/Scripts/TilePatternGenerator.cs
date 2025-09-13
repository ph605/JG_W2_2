using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

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

    [Tooltip("장애물이 생성될 X축 범위 (최소, 최대)")]
    public Vector2 xBounds = new Vector2(-15, 15);
    [Tooltip("장애물이 생성될 Y축 범위 (최소, 최대)")]
    public Vector2 yBounds = new Vector2(0, 10);
    [Tooltip("장애물이 생성될 Z축 범위 (최소, 최대)")]
    public Vector2 zBounds = new Vector2(-15, 15);

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
            // 사용 가능한 장애물 타입 목록 가져오기
            var obstacleTypes = System.Enum.GetValues(typeof(ObstacleType));
            // 무작위로 장애물 타입 선택
            ObstacleType randomType = (ObstacleType)obstacleTypes.GetValue(Random.Range(0, obstacleTypes.Length));

            // 무작위 위치 생성
            float randomX = Random.Range(xBounds.x, xBounds.y);
            float randomY = Random.Range(yBounds.x, yBounds.y);
            float randomZ = Random.Range(zBounds.x, zBounds.y);
            Vector3 randomPosition = new Vector3(randomX, randomY, randomZ);

            // 무작위 회전값 생성
            Vector3 randomRotation = Vector3.zero;
            if (randomizeYRotation)
            {
                randomRotation.y = Random.Range(0, 360);
            }

            // 무작위 크기 생성
            float scaleValue = Random.Range(randomScaleRange.x, randomScaleRange.y);
            Vector3 randomScale = new Vector3(scaleValue, scaleValue, scaleValue);

            // 생성된 무작위 데이터로 ObstacleData 구조체 채우기
            obstacleLayout.Add(new ObstacleData(randomType, randomPosition, randomRotation, randomScale));
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
}
