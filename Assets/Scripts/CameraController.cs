using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField]
    Transform target;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void LateUpdate()
    {
        transform.position = target.position + new Vector3(0, 0, -8.5f);
    }
}
