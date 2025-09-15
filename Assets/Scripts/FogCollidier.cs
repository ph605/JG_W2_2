using UnityEngine;

public class FogCollidier : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            collision.transform.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
            collision.transform.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
            collision.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0,0,0));
        }
    }
}
