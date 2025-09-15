using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ClearUI : MonoBehaviour
{
    public TextMeshProUGUI textTitle;
    public Button buttonLobby;
    public Button buttonRestart;
    public Button buttonNextStage;
    public Button buttonExit;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        buttonLobby.onClick.AddListener(() => SceneManager.instance.LoadLobbyScene());
        buttonRestart.onClick.AddListener(() => SceneManager.instance.ReloadCurrentScene());
        buttonNextStage.onClick.AddListener(() => SceneManager.instance.LoadNextScene());
        buttonExit.onClick.AddListener(() => SceneManager.instance.QuitGame());
    }

    public void SetGame(bool isClear)
    {
        textTitle.text = isClear ? "Clear" : "Fail";
        buttonNextStage.enabled = isClear;
    }

}
