using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InGameUIManager : MonoBehaviour
{
    public static InGameUIManager instance;

    public TextMeshProUGUI timer;
    public TextMeshProUGUI score;

    public GameObject clearUI;
    public GameObject configurationUI;

    private float time = 0f;
    private int scoreValue = 0;
    private bool useTimer = true;

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

    void InitData()
    {
        clearUI.SetActive(false);
        configurationUI.SetActive(false);
        scoreValue = 0;
        time = 0f;
        AddScore(0);
        Time.timeScale = 1f;
        useTimer = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (timer != null && useTimer)
            timeCalculate();

        if (Input.GetKeyDown(KeyCode.Escape))
            SetActiveConfiguration();
    }

    // ESC 키를 누를 때, 옵션 UI 출력/숨김
    private void SetActiveConfiguration()
    {
        if (configurationUI.gameObject.activeSelf)
        {
            configurationUI.gameObject.SetActive(false);
            MouseLock(true);
        }
        else
        {
            configurationUI.gameObject.SetActive(true);
            MouseLock(false);
        }
    }

// 타이머 시간 계산 및 출력
private void timeCalculate()
    {
        time += Time.deltaTime;
        int min = Mathf.FloorToInt(time / 60f);
        int sec = Mathf.FloorToInt(time % 60f);
        timer.text = string.Format("Timer : {0:00}:{1:00}", min, sec);
    }

    // 점수 증가 및 출력
    public void AddScore(int addValue)
    {
        scoreValue += addValue;
        score.text = string.Format("Score : {0}", scoreValue);
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

    // 클리어 UI 출력
    public void ClearUI(bool isClear)
    {
        Time.timeScale = 0;
        MouseLock(true);
        useTimer = false;

        clearUI.GetComponent<ClearUI>().SetGame(isClear);
    }
}
