using UnityEngine;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    public Button buttonTutorial;
    public Button buttonStage1;
    public Button buttonStage2;
    public Button buttonStage3;
    public Button buttonStageChallenge;

    void Awake()
    {
        // 2. 각 버튼이 클릭되었을 때 실행될 함수를 연결(등록)합니다.
        // AddListener를 사용하면 인스펙터의 OnClick()을 설정할 필요가 없습니다.

        // 튜토리얼 버튼 클릭 시 Tutorial 씬 로드
        buttonTutorial.onClick.AddListener(() => LoadScene(SceneType.Tutorial));

        // 1스테이지 버튼 클릭 시 Stage1 씬 로드
        buttonStage1.onClick.AddListener(() => LoadScene(SceneType.Stage1));

        // 2스테이지 버튼 클릭 시 Stage2 씬 로드
        buttonStage2.onClick.AddListener(() => LoadScene(SceneType.Stage2));

        // 3스테이지 버튼 클릭 시 Stage3 씬 로드
        buttonStage3.onClick.AddListener(() => LoadScene(SceneType.Stage3));

        // 챌린지 버튼 클릭 시 StageChallenge 씬 로드
        buttonStageChallenge.onClick.AddListener(() => LoadScene(SceneType.StageChallenge));
    }

    // 3. 씬을 실제로 로드하는 함수입니다.
    private void LoadScene(SceneType scene)
    {
        // 이전에 만든 SceneManager 싱글턴을 호출합니다.
        // enum 멤버를 문자열로 변환하여 전달합니다. (예: SceneType.Stage1 -> "Stage1")
        SceneManager.instance.LoadSceneByName(scene.ToString());
    }
}
