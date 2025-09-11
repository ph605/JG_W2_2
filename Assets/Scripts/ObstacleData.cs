using UnityEngine;

[System.Serializable]
public struct ObstacleData
{
    public ObstacleType type;
    public Vector3 position;
    public Vector3 eulerAngles;
    public Vector3 scale;
    public ObstacleData(ObstacleType _type, Vector3 _pos, Vector3 _rot, Vector3 _scale)
    {
        type = _type;
        position = _pos;
        eulerAngles = _rot;
        scale = _scale;
    }
}
