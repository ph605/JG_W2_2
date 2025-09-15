using UnityEngine;

public class TutorialMessage : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        int stageNum = SceneManager.instance.GetCurrentSceneNum();
        if (stageNum <= 1)
            gameObject.SetActive(false);
        else
            gameObject.SetActive(true);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
