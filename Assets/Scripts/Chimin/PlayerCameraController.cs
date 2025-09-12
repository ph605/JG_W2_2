using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCameraController : MonoBehaviour
{
    // =================================================================
    // Target & References
    // =================================================================
    [Header("Target & References")]
    [SerializeField] Transform playerTransform;
    [SerializeField] PlayerController playerController;

    // =================================================================
    // Camera Control
    // =================================================================
    [Header("Camera Control")]
    [SerializeField] float cameraRotationSpeed = 0.05f;
    [SerializeField] float minPitch = -20f;
    [SerializeField] float maxPitch = 60f;
    [SerializeField] float cameraDistance = 10f;
    [SerializeField] Vector3 pivotOffset = new Vector3(0, 1.5f, 0);

    // =================================================================
    // Dynamic Adjustments (인스펙터 스크린샷 기준)
    // =================================================================
    [Header("Dynamic Adjustments")]
    [Tooltip("카메라가 최대 각도로 '위'를 볼 때, 중심축(Pivot)이 얼마나 '아래로' 내려갈지 설정합니다.")]
    [SerializeField] float maxPitchVerticalDisplacement = -0.6f;
    [Tooltip("카메라가 최대 각도로 '위'를 볼 때, 플레이어와의 최소 거리를 설정합니다.")]
    [SerializeField] float minDistanceAtMaxPitch = 6.6f;
    [Tooltip("카메라가 최대 각도로 '위'를 볼 때, 시야각(FOV)을 얼마나 넓힐지 설정합니다.")]
    [SerializeField] float maxPitchFov = 30f;
    [Tooltip("카메라가 최대 각도로 '아래'를 볼 때, 시야각(FOV)을 얼마나 넓힐지 설정합니다. (추가된 기능)")]
    [SerializeField] float maxDownwardPitchFov = 85f;

    // =================================================================
    // Camera Collision
    // =================================================================
    [Header("Camera Collision")]
    [SerializeField] LayerMask cameraCollisionMask = ~0;
    [SerializeField] float minCameraDistance = 0.3f;
    [SerializeField] float cameraProbeRadius = 8f;
    [SerializeField] float cameraCollisionLerp = 50f;
    float currentCamDist;

    // =================================================================
    // Camera FX
    // =================================================================
    [Header("Camera FX")]
    [SerializeField] float chargeShakeMax = 0.15f;
    [SerializeField] float chargeShakeFreq = 18f;
    [SerializeField] float speedShakeIntensity = 0.12f;
    [SerializeField] float speedShakeFreq = 22f;
    [SerializeField] float burstShakeIntensity = 0.35f;
    [SerializeField] float burstShakeDecay = 4f;
    [SerializeField] float posJitter = 0.05f;
    [SerializeField] float rotJitter = 1.5f;
    [SerializeField] float launchKickBack = 0.6f;
    [SerializeField] float launchKickTime = 0.15f;
    [SerializeField] float hasteFov = 72f;
    [SerializeField] float fovLerp = 6f;

    // =================================================================
    // Swing Settings (새로 추가)
    // =================================================================
    [Header("Swing Settings")]
    [SerializeField] float swingFov = 90f;

    [Tooltip("스윙 시 플레이어를 화면 좌/우로 얼마나 치우치게 할지 결정합니다.")]
    [SerializeField] float swingPivotOffsetXAmount = 2.5f;
    [Tooltip("피봇 오프셋이 적용되고 돌아오는 속도입니다.")]
    [SerializeField] float swingPivotOffsetLerp = 8f;




    // =================================================================
    // Private Variables
    // =================================================================
    private Camera mainCamera;
    private float cameraPitch = 0f;
    private float yaw = 0f;
    private float burstShakeTimer = 0f;
    private float launchKickTimer = 0f;
    private float baseFov;
    private float perlinSeed;
    private float currentUpwardPitchT = 0f;
    private float currentDownwardPitchT = 0f;  
    // 스윙 상태 관리를 위한 변수
    private bool isSwinging = false;


    // private float swingPivotOffsetX = 0f; // 이 줄을 아래 두 줄로 나눠서 관리합니다.
    // ▼▼▼ 아래 2줄로 교체 ▼▼▼
    private float currentSwingPivotOffsetX = 0f; // 실제 적용될 현재 피봇 오프셋
    private float targetSwingPivotOffsetX = 0f;  // 도달해야 할 목표 피봇 오프셋

    // =================================================================
    // Unity Methods
    // =================================================================
    void Awake()
    {
        mainCamera = GetComponent<Camera>();
        if (playerTransform == null)
            Debug.LogError("Player Transform이 PlayerCameraController에 할당되지 않았습니다.");
        if (playerController == null)
            Debug.LogError("PlayerController가 PlayerCameraController에 할당되지 않았습니다.");
    }

    void Start()
    {
        currentCamDist = cameraDistance;
        baseFov = mainCamera ? mainCamera.fieldOfView : 60f;
        perlinSeed = Random.value * 1000f;

        if (playerTransform)
            yaw = playerTransform.eulerAngles.y;
    }

    void LateUpdate()
    {
        if (playerTransform == null || playerController == null) return;
        if (playerController.IsClinging) return;

        if (!playerController.WasHasteActive && playerController.IsHasteActive)
        {
            burstShakeTimer = 1f;
            launchKickTimer = launchKickTime;
        }

        ProcessLook();
    }



    /// <summary>
    /// 스윙 카메라 모드를 시작합니다.
    /// </summary>
    public void EnterSwingView()
    {
        isSwinging = true;
    }

    /// <summary>
    /// 스윙 카메라 모드를 종료합니다.
    /// </summary>
    public void ExitSwingView()
    {
        isSwinging = false;
        // swingPivotOffsetX = 0f; // 이 줄을 아래 줄로 교체합니다.
        targetSwingPivotOffsetX = 0f; // 스윙이 끝나면 목표 지점을 다시 0으로 설정합니다.
    }

    // =================================================================
    // Core Logic
    // =================================================================
    void ProcessLook()
    {
        // 스윙 중일 때 A/D 키 입력을 확인합니다.
        if (isSwinging)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed)
                {
                    // A키를 누르면 카메라를 왼쪽으로 이동
                    targetSwingPivotOffsetX = -swingPivotOffsetXAmount;
                }
                else if (kb.dKey.isPressed)
                {
                    // D키를 누르면 카메라를 오른쪽으로 이동
                    targetSwingPivotOffsetX = swingPivotOffsetXAmount;
                }
                else
                {
                    // 아무 키도 누르지 않으면 중앙으로 복귀
                    targetSwingPivotOffsetX = 0f;
                }
            }
        }

        // '현재값'이 '목표값'을 향해 부드럽게 따라갑니다.
        currentSwingPivotOffsetX = Mathf.Lerp(currentSwingPivotOffsetX, targetSwingPivotOffsetX, Time.deltaTime * swingPivotOffsetLerp);

        yaw = playerTransform.eulerAngles.y;

        // 스윙 중이 아닐 때만 마우스 입력을 받습니다.
        if (!isSwinging)
        {
            cameraPitch -= playerController.LookInput.y * cameraRotationSpeed;
            cameraPitch = Mathf.Clamp(cameraPitch, minPitch, maxPitch);
        }

        currentUpwardPitchT = 0f;
        currentDownwardPitchT = 0f;

        if (cameraPitch < 0) // 위를 볼 때
        {
            currentUpwardPitchT = Mathf.InverseLerp(0f, minPitch, cameraPitch);
        }
        else if (cameraPitch > 0) // 아래를 볼 때
        {
            currentDownwardPitchT = Mathf.InverseLerp(0f, maxPitch, cameraPitch);
        }

        float dynamicHeight = Mathf.Lerp(0, maxPitchVerticalDisplacement, currentUpwardPitchT);
        Vector3 horizontalOffset = playerTransform.right * currentSwingPivotOffsetX;
        Vector3 adjustedPivot = playerTransform.position + pivotOffset - new Vector3(0, dynamicHeight, 0) + horizontalOffset;
        float dynamicDistance = Mathf.Lerp(cameraDistance, minDistanceAtMaxPitch, currentUpwardPitchT);
        Quaternion camRot = Quaternion.Euler(cameraPitch, yaw, 0f);

        HandleCameraCollisionAndPositioning(adjustedPivot, camRot, dynamicDistance);
        transform.LookAt(adjustedPivot);
        ApplyFovEffect();
        ApplyCameraShake();
    }

    void HandleCameraCollisionAndPositioning(Vector3 pivot, Quaternion camRot, float desiredDist)
    {
        Vector3 desiredDir = camRot * Vector3.back;
        float targetDist = desiredDist;

        if (Physics.SphereCast(
            pivot, cameraProbeRadius, desiredDir, out RaycastHit hit,
            desiredDist, cameraCollisionMask, QueryTriggerInteraction.Ignore))
        {
            targetDist = Mathf.Max(minCameraDistance, hit.distance - cameraProbeRadius);
        }

        ApplyLaunchKick(ref targetDist);

        currentCamDist = Mathf.Lerp(
            currentCamDist, targetDist, 1f - Mathf.Exp(-cameraCollisionLerp * Time.deltaTime));

        transform.position = pivot + desiredDir * currentCamDist;
    }

    // =================================================================
    // Camera Effects
    // =================================================================
    void ApplyFovEffect()
    {
        // 1. 기본 목표 FOV 계산 (질주, 상하 시점)
        float hasteTargetFov = playerController.IsHasteActive ? hasteFov : baseFov;
        float upwardPitchTargetFov = Mathf.Lerp(baseFov, maxPitchFov, currentUpwardPitchT);
        float downwardPitchTargetFov = Mathf.Lerp(baseFov, maxDownwardPitchFov, currentDownwardPitchT);

        float defaultTargetFov = Mathf.Max(hasteTargetFov, upwardPitchTargetFov, downwardPitchTargetFov);

        // 2. 스윙 중인지 확인하여 최종 목표 FOV 결정
        float finalTargetFov = isSwinging ? swingFov : defaultTargetFov;

        // 3. 최종 목표 FOV로 부드럽게 변경
        mainCamera.fieldOfView = Mathf.Lerp(
            mainCamera.fieldOfView, finalTargetFov, 1f - Mathf.Exp(-fovLerp * Time.deltaTime));
    }

    void ApplyLaunchKick(ref float targetDist)
    {
        if (launchKickTimer <= 0f) return;
        float k = Mathf.Clamp01(launchKickTimer / launchKickTime);
        targetDist += launchKickBack * k;
        launchKickTimer = Mathf.Max(0f, launchKickTimer - Time.deltaTime);
    }

    void ApplyCameraShake()
    {
        float amp = 0f;
        float t = Time.time;

        if (IsHasteCharging())
        {
            float chargeT = Mathf.Clamp01(playerController.HasteHoldTimer / playerController.HasteHoldDuration);
            amp += chargeShakeMax * chargeT;
        }

        if (burstShakeTimer > 0f)
        {
            amp += burstShakeIntensity * burstShakeTimer;
            burstShakeTimer = Mathf.MoveTowards(burstShakeTimer, 0f, burstShakeDecay * Time.deltaTime);
        }

        if (playerController.IsHasteActive)
        {
            amp += speedShakeIntensity;
        }

        if (amp <= 0f) return;

        float nx = (Mathf.PerlinNoise(perlinSeed + t * chargeShakeFreq, 0f) - 0.5f) * 2f;
        float ny = (Mathf.PerlinNoise(perlinSeed + 100f + t * speedShakeFreq, 0f) - 0.5f) * 2f;

        Vector3 posOffset = new Vector3(nx, ny, 0f) * posJitter * amp;
        Vector3 rotOffset = new Vector3(ny, 0f, -nx) * rotJitter * amp;

        transform.position += transform.TransformDirection(posOffset);
        transform.rotation *= Quaternion.Euler(rotOffset);
    }

    private bool IsHasteCharging()
    {
        return !playerController.IsHasteActive && playerController.HasteHoldTimer > 0;
    }
}