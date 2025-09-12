using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OptionManager : MonoBehaviour
{
    public static OptionManager instance;

    // 인스펙터에서 연결할 UI 요소들
    public Slider mouseSensitivitySlider;
    public TextMeshProUGUI sensitivityValueText;

    public Button buttonESC;
    public Button buttonRestart;
    public Button buttonLobby;
    public Button buttonExit;

    // 실제 마우스 감도 값 (다른 스크립트에서 이 값을 참조합니다)
    public float mouseSensitivity = 100f;

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

        // 슬라이더 값이 변경될 때마다 OnSensitivityChanged 함수를 호출하도록 리스너 등록
        mouseSensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        buttonESC.onClick.AddListener(() => CloseConfiguration());
        buttonRestart.onClick.AddListener(() => SceneManager.instance.ReloadCurrentScene());
        buttonLobby.onClick.AddListener(() => SceneManager.instance.LoadLobbyScene());
        buttonExit.onClick.AddListener(() => SceneManager.instance.QuitGame());

        gameObject.SetActive(false);
    }

    void Start()
    {
        // 게임 시작 시 슬라이더와 텍스트 초기화
        mouseSensitivitySlider.value = (mouseSensitivity - 50f) / 100f;
        UpdateSensitivityText(mouseSensitivity);
    }

    // 슬라이더 값이 변경될 때 호출되는 함수
    public void OnSensitivityChanged(float value)
    {
        // 감도 값 업데이트
        mouseSensitivity = value * 100f + 50f;
        // 텍스트 업데이트
        UpdateSensitivityText(mouseSensitivity);
    }

    // 감도 텍스트를 업데이트하는 함수
    private void UpdateSensitivityText(float value)
    {
        // 소수점 두 자리까지만 표시 (예: 1.25)
        sensitivityValueText.text = ((int)value).ToString();
    }

    private void CloseConfiguration()
    {
        transform.gameObject.SetActive(false);
    }
}
