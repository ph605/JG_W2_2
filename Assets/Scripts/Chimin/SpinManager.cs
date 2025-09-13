using UnityEngine;
using TMPro;

public class SpinManager : MonoBehaviour
{
    // 다른 스크립트에서 SpinManager를 쉽게 참조할 수 있도록 static instance를 만듭니다 (싱글톤 패턴).
    public static SpinManager instance;

    [Header("UI")]
    [SerializeField] private TMP_Text totalRotationText;

    private int totalRotations = 0;

    void Awake()
    {
        // 싱글톤 인스턴스 설정
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 게임 시작 시 UI 초기화
        UpdateRotationUI();
    }

    void OnEnable()
    {
        // PlayerGrapple이 방송하는 "한 바퀴 돌았음" 이벤트를 구독합니다.
        RotationTracker.OnRotationComplete += HandleRotationComplete;
    }

    void OnDisable()
    {
        // 오브젝트가 비활성화될 때 구독을 해제합니다.
        RotationTracker.OnRotationComplete -= HandleRotationComplete;
    }

    // 이벤트가 발생했을 때 호출될 함수
    private void HandleRotationComplete()
    {
        totalRotations++;
        Debug.Log($"[SpinManager] +1 spin, total={totalRotations}");
        UpdateRotationUI();
    }

    // UI 텍스트를 업데이트하는 함수
    private void UpdateRotationUI()
    {
        if (totalRotationText != null)
        {
            totalRotationText.text = "Total Rotations: " + totalRotations;
        }
    }
}
