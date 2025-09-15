using NUnit.Framework;
using System.Threading;
using TMPro;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[System.Serializable]
public struct GoalValue
{
    public float time;
    public int score;
    public bool isTutorial;
};


public class InGameUIManager : MonoBehaviour
{
    public static InGameUIManager instance;

    public TextMeshProUGUI timer;
    public TextMeshProUGUI score;
    public GameObject textInfo;

    // 스테이지별로 목표 점수
    public List<GoalValue> goalValue;

    public GameObject clearUI;
    public GameObject configurationUI;
    public GameObject Crosshair;

    private float time = 5000f;
    private int scoreValue = 0;
    private bool useTimer = true;

    private int stage = 0;
    private GoalValue currentGoalValue;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            InGameUIManager.instance.InitData();
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        InitData();
    }

    void InitData()
    {
        stage = SceneManager.instance.GetCurrentSceneNum();
        currentGoalValue = goalValue[stage];
        scoreValue = 0;
        time = currentGoalValue.time;

        clearUI.SetActive(false);
        Crosshair.SetActive(true);
        configurationUI.SetActive(false);
        AddScore(0);

        Time.timeScale = 1f;
        useTimer = (currentGoalValue.isTutorial == false);

        int stageNum = SceneManager.instance.GetCurrentSceneNum();
        textInfo.SetActive(stageNum > 1);
    }

    // Update is called once per frame
    void Update()
    {
        if (timer != null && useTimer)
            timeCalculate();

        if (Input.GetKeyDown(KeyCode.Escape))
            SetActiveConfiguration();

        CheckChallengeModeClear();
    }

    private void CheckChallengeModeClear()
    {
        int targetValue = currentGoalValue.score;

        if (targetValue > 3000 && scoreValue >= targetValue)
            ClearUI(true);

    }

    // ESC 키를 누를 때, 옵션 UI 출력/숨김
    private void SetActiveConfiguration()
    {
        if (configurationUI.gameObject.activeSelf)
        {
            configurationUI.gameObject.SetActive(false);
            MouseLock(true);
            Crosshair.SetActive(true);
        }
        else
        {
            configurationUI.gameObject.SetActive(true);
            MouseLock(false);
            Crosshair.SetActive(false);
        }
    }

// 타이머 시간 계산 및 출력
private void timeCalculate()
    {
        time -= Time.deltaTime;
        int min = Mathf.FloorToInt(time / 60f);
        int sec = Mathf.FloorToInt(time % 60f);
        timer.text = string.Format("시간 : {0:00}:{1:00}", min, sec);

        if (time < 0f)
            ClearUI(false);
    }

    // 점수 증가 및 출력
    public void AddScore(int addValue)
    {
        scoreValue += addValue;
        int targetScore = currentGoalValue.score;

        if(targetScore != 0)
            score.text = string.Format("점수 : {0}/{1}", scoreValue, targetScore);
        else
            score.text = string.Format("점수 : {0}", scoreValue);
    }

    // 마우스 잠금/해제 설정
    public void MouseLock(bool isLock)
    {
        if (isLock)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        else
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    // 현재 스테이지를 클리어했는지 확인
    public void CheckClearStage()
    {
        // 튜토리얼은 반드시 통과
        if (currentGoalValue.isTutorial)
        {
            ClearUI(true);
            return;
        }

        // 목표에 도달해서 바로 통과
        ClearUI(true);
    }

    // 클리어 UI 출력
    private void ClearUI(bool isClear)
    {
        Time.timeScale = 0;
        MouseLock(false);
        useTimer = false;

        clearUI.SetActive(true);
        clearUI.GetComponent<ClearUI>().SetGame(isClear);
    }
}
