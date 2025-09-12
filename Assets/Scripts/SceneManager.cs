using System;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum SceneType
{ 
    Lobby,
    Tutorial,
    Stage1,
    Stage2,
    Stage3,
    StageChallenge
};


public class SceneManager : MonoBehaviour
{
    public static SceneManager instance;

    void Awake()
    {
        // 싱글턴 패턴 구현
        if (instance != null && instance != this)
        {
            // 이미 인스턴스가 존재하면 새로 생긴 것을 파괴
            Destroy(gameObject);
            return;
        }
        // 이 오브젝트를 유일한 인스턴스로 설정
        instance = this;

        // 씬이 전환되어도 이 오브젝트는 파괴되지 않음
        DontDestroyOnLoad(gameObject);
    }

    public void LoadSceneByName(string sceneName)
    {
        // 이름 충돌을 피하기 위해 전체 경로를 명시
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }

    public void LoadLobbyScene()
    {
        // 이름 충돌을 피하기 위해 전체 경로를 명시
        UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
    }

    public void ReloadCurrentScene()
    {
        // 이름 충돌을 피하기 위해 전체 경로를 명시
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        UnityEngine.SceneManagement.SceneManager.LoadScene(currentSceneName);
    }

    public void LoadNextScene()
    {
        // 이름 충돌을 피하기 위해 전체 경로를 명시
        int currentSceneIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
        int nextSceneIndex = currentSceneIndex + 1;

        if (nextSceneIndex >= UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings)
        {
            LoadLobbyScene();
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneIndex);
        }
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        // 유니티 에디터에서 실행 중일 경우, 플레이 모드를 중지합니다.
        EditorApplication.isPlaying = false;
#else
        // 빌드된 게임에서 실행 중일 경우, 어플리케이션을 종료합니다.
        Application.Quit();
#endif
    }

    // 현재 스테이지 이름 가져오기
    public SceneType GetCurrentStage()
    {
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        if (Enum.TryParse(currentSceneName, out SceneType result))
        {
            return result;
        }
        else
        {
            // 만약 씬 이름과 일치하는 enum 멤버가 없다면 Unknown을 반환합니다.
            Debug.LogWarning($"'{currentSceneName}' 씬과 일치하는 SceneType을 찾을 수 없습니다.");
            return SceneType.Lobby;
        }
    }
}
