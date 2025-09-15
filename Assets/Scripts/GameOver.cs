using UnityEngine;

public class GameOver : MonoBehaviour
{
    [SerializeField] private GameObject target;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(target.transform.position.y < -50)
        {
            SceneManager.instance.ReloadCurrentScene();
        }
    }
}
