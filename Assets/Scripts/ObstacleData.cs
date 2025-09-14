using UnityEngine;

[System.Serializable]
public struct ObstacleData
{
    public int id;
    public ObstacleType type;
    public Vector3 position;
    public Vector3 eulerAngles;
    public Vector3 scale;
    public bool isActive;

    public ObstacleData(int _id, ObstacleType _type, Vector3 _pos, Vector3 _rot, Vector3 _scale, bool _isActive)
    {
        id = _id;
        type = _type;
        position = _pos;
        eulerAngles = _rot;
        scale = _scale;
        isActive = _isActive;
    }
}
