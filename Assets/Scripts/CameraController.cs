using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Core Settings")]
    [SerializeField]
    private Transform target; // 플레이어 Transform


[Header("Camera Position")]
    [SerializeField]
    private Vector3 offset = new Vector3(0, 0, -8.5f); // 플레이어로부터의 거리

    [Header("Collision Settings")]
    [Tooltip("카메라가 충돌을 감지할 레이어를 선택합니다.")]
    [SerializeField]
    private LayerMask collisionLayers; // 벽으로 설정된 레이어
    [Tooltip("카메라의 충돌 판정 크기입니다.")]
    [SerializeField]
    private float cameraCollisionRadius = 0.2f; // 충돌 감지용 구체의 반지름

    [Header("FOV Settings")]
    [SerializeField]
    private float normalFov = 60f; // 평상시 시야각
    [SerializeField]
    private float swingFov = 90f; // 스윙 시 시야각
    [SerializeField]
    private float fovTransitionSpeed = 5f; // 시야각 변경 속도

    [Header("Look At Settings")]
    [Tooltip("0이면 전혀 바라보지 않고, 1이면 완전히 해당 지점을 바라봅니다.")]
    [Range(0f, 1f)]
    [SerializeField]
    private float lookAtStrength = 0.4f; // 스윙 지점을 바라보는 강도
    [SerializeField]
    private float lookAtTransitionSpeed = 5f; // 방향 전환 속도

    // 내부 변수
    private Camera _camera;
    private float _targetFov;
    private Quaternion _originalRotation;
    private Quaternion _targetRotation;

    void Start()
    {
        // 초기화
        _camera = GetComponent<Camera>();
        if (_camera == null)
        {
            Debug.LogError("CameraController에 Camera 컴포넌트가 없습니다!");
        }
        _targetFov = normalFov;
        _originalRotation = transform.rotation;
        _targetRotation = _originalRotation;
    }

    void LateUpdate()
    {
        // 1. 목표 시야각과 회전 값을 부드럽게 갱신합니다.
        _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, _targetFov, Time.deltaTime * fovTransitionSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, Time.deltaTime * lookAtTransitionSpeed);

        // 2. 회전이 적용된 상태에서, 카메라가 원래 있어야 할 '목표 위치'를 계산합니다.
        Vector3 desiredPosition = target.position + (transform.rotation * offset);

        // 3. 플레이어 위치에서 '목표 위치' 방향으로 스피어캐스트를 발사해 장애물을 확인합니다.
        Vector3 castDirection = desiredPosition - target.position;
        float castDistance = castDirection.magnitude;
        castDirection.Normalize();

        RaycastHit hit;
        if (Physics.SphereCast(target.position, cameraCollisionRadius, castDirection, out hit, castDistance, collisionLayers))
        {
            // 4-A. 만약 장애물이 감지되었다면, 카메라의 위치를 충돌 지점으로 설정합니다.
            // (충돌 지점보다 살짝 앞으로 당겨서 카메라가 벽에 파묻히지 않게 함)
            transform.position = target.position + castDirection * (hit.distance - cameraCollisionRadius * 0.5f);
        }
        else
        {
            // 4-B. 장애물이 없다면, 원래 목표했던 위치로 카메라를 이동합니다.
            transform.position = desiredPosition;
        }
    }

    /// <summary>
    /// 스윙 시작 시 호출될 메서드
    /// </summary>
    public void EnterSwingView(Vector3 hookPoint)
    {
        _targetFov = swingFov;

        // 카메라 위치에서 훅 지점까지의 방향을 계산하여 목표 회전 값 설정
        Vector3 directionToHook = hookPoint - transform.position;
        Quaternion lookRotation = Quaternion.LookRotation(directionToHook);
        _targetRotation = Quaternion.Slerp(_originalRotation, lookRotation, lookAtStrength);
    }

    /// <summary>
    /// 스윙 종료 시 호출될 메서드
    /// </summary>
    public void ExitSwingView()
    {
        _targetFov = normalFov;
        _targetRotation = _originalRotation;
    }



}