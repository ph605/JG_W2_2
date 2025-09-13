using UnityEngine;

public class CameraRigFollow : MonoBehaviour
{
    [Header(" 추적할 대상 ")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private PlayerController playerController;

    [Header("Tumble Cam Options")]
    [Tooltip("Tumbling 중일 때 카메라 회전을 고정할지 여부")]
    [SerializeField] private bool freezeRotationWhileTumbling = true;
    [Tooltip("Tumbling 해제 후 플레이어 회전에 부드럽게 재동기화할 속도(0이면 즉시 스냅)")]
    [SerializeField] private float unfreezeLerpSpeed = 10f;

    private bool wasTumbling = false;
    private Quaternion frozenRotation; // Tumbling 시작 시점의 카메라 회전

    void Update()
    {
        if (playerTransform == null || playerController == null) return;

        // 위치는 항상 플레이어를 따라감
        transform.position = playerTransform.position;

        bool tumbling = playerController.IsTumbling;

        if (freezeRotationWhileTumbling && tumbling)
        {
            // Tumbling 진입 프레임에 현재 회전을 저장
            if (!wasTumbling)
            {
                frozenRotation = transform.rotation;
            }

            // Tumbling 동안엔 회전을 저장된 값으로 고정
            transform.rotation = frozenRotation;
        }
        else
        {
            // 평상시/스윙 중: 플레이어 회전을 그대로 복사
            // (원래처럼 스윙 기울기 반영)
            if (!freezeRotationWhileTumbling || !wasTumbling || unfreezeLerpSpeed <= 0f)
            {
                transform.rotation = playerTransform.rotation;
            }
            else
            {
                // Tumbling 종료 직후엔 살짝 부드럽게 이어 붙이기(옵션)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    playerTransform.rotation,
                    Time.deltaTime * unfreezeLerpSpeed
                );
            }
        }

        wasTumbling = tumbling;
    }
}
