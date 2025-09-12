using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            SetActiveConfiguration();
    }

    // ESC 키를 누를 때, 옵션 UI 출력/숨김
    private void SetActiveConfiguration()
    {
        if (OptionManager.instance.gameObject.activeSelf)
            OptionManager.instance.gameObject.SetActive(false);
        else
            OptionManager.instance.gameObject.SetActive(true);
    }
}
