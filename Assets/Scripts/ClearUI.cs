using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ClearUI : MonoBehaviour
{
    public TextMeshPro textTitle;
    public Button buttonLobby;
    public Button buttonRestart;
    public Button buttonNextStage;
    public Button buttonExit;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        buttonLobby.onClick.AddListener(() => LoadScene(SceneType.Lobby));
        buttonRestart.onClick.AddListener(() => LoadScene(SceneManager.instance.GetCurrentStage()));
        buttonNextStage.onClick.AddListener(() => SceneManager.instance.LoadNextScene());
        buttonExit.onClick.AddListener(() => SceneManager.instance.QuitGame());
    }

    private void LoadScene(SceneType scene)
    {
        SceneManager.instance.LoadSceneByName(scene.ToString());
    }

}
