using UnityEngine;

[CreateAssetMenu(fileName = "NewTilePattern", menuName = "Map/Tile Pattern")]
public class TilePattern : ScriptableObject
{
    [Tooltip("이 패턴에 배치될 장애물들의 정보 배열")]
    public ObstacleData[] obstacleLayout;

    // 필요하다면 타일의 길이나 난이도 같은 추가 정보도 넣을 수 있습니다.
    // public float tileLength = 50f;
    // public int difficulty = 1;

}
