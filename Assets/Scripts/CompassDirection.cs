using UnityEngine;
using UnityEngine.UI;

public class CompassDirection : MonoBehaviour
{
    public RectTransform compassNeedle;

    private Transform mainCameraTransform;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        mainCameraTransform = Camera.main.transform;

        if (compassNeedle == null)
            compassNeedle = GetComponent<RectTransform>();
    }

    // Update is called once per frame
    void Update()
    {
        if (compassNeedle != null && mainCameraTransform != null)
            UpdateCompass();
    }

    /// 카메라의 방향에 따라 나침반 바늘을 회전시키는 함수
    private void UpdateCompass()
    {
        // 1. 카메라가 월드 공간에서 바라보는 방향을 가져옴 (Y축은 무시하여 수평 방향만 고려)
        Vector3 cameraForward = mainCameraTransform.forward;
        cameraForward.y = 0; // 수직 시점 변화에 영향을 받지 않도록 y값을 0으로 설정

        // 2. 월드 공간의 북쪽 방향 (Vector3.forward)을 기준으로 카메라 방향까지의 각도를 계산
        // Vector3.SignedAngle(기준 벡터, 목표 벡터, 축) -> 축을 기준으로 시계/반시계 방향을 고려한 각도 반환
        float angle = Vector3.SignedAngle(Vector3.forward, cameraForward, Vector3.up);

        // 3. 나침반 UI의 초기 각도(45도)를 보정
        // 나침반의 'N'이 정북(0도)을 가리키도록 하려면, 계산된 각도에서 45도를 빼줘야 함
        // 최종적으로 UI 회전은 반대 방향으로 이루어져야 하므로 -angle을 사용
        float finalAngle = -angle + 45f;

        // 4. 나침반 바늘의 Z축 회전값을 변경
        compassNeedle.rotation = Quaternion.Euler(0, 0, finalAngle);
    }
}
