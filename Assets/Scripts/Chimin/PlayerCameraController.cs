using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)]
public class PlayerCameraController : MonoBehaviour
{
    [Header("Target & References")]
    [Tooltip("카메라가 따라갈 목표입니다. 플레이어 본체가 아닌, 회전하지 않는 자식 오브젝트(CameraTarget)를 할당해야 합니다.")]
    [SerializeField] Transform cameraTarget;
    [SerializeField] PlayerController playerController;

    [Header("Camera Control")]
    [SerializeField] float cameraRotationSpeed = 0.05f;
    [SerializeField] float minPitch = -20f;
    [SerializeField] float maxPitch = 60f;
    [SerializeField] float cameraDistance = 10f;
    [SerializeField] Vector3 pivotOffset = new Vector3(0, 1.5f, 0);

    [Header("Dynamic Adjustments")]
    [SerializeField] float maxPitchVerticalDisplacement = -0.6f;
    [SerializeField] float minDistanceAtMaxPitch = 6.6f;
    [SerializeField] float maxPitchFov = 30f;
    [SerializeField] float maxDownwardPitchFov = 85f;

    [Header("Camera Collision")]
    [SerializeField] LayerMask cameraCollisionMask = ~0;
    [SerializeField] float minCameraDistance = 0.3f;
    [SerializeField] float cameraProbeRadius = 8f;
    [SerializeField] float cameraCollisionLerp = 50f;
    float currentCamDist;

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

    [Header("Swing Settings")]
    [SerializeField] float swingFov = 90f;
    [SerializeField] float swingPivotOffsetXAmount = 2.5f;
    [SerializeField] float swingPivotOffsetLerp = 8f;

    [Header("Tumble Orbit")]
    [SerializeField] bool freeOrbitWhileTumbling = true;

    [Header("Release Orbit Continuity")]
    [Tooltip("홀딩을 떼는 직후 카메라가 계속 현재 시점을 유지(자유 오빗)할지")]
    [SerializeField] bool keepFreeOrbitAfterClingRelease = true;
    [Tooltip("홀딩 해제 후 자유 오빗을 유지할 시간(초)")]
    [SerializeField] float releaseOrbitDuration = 0.5f;

    [Header("SuperJump Blend")]
    [SerializeField] float superJumpFreeOrbitHold = 0.25f; // 슈점 정렬 동안 자유 오빗 유지
    [SerializeField] float superJumpResyncTime = 0.35f;    // 정렬 직후 타겟 Yaw로 서서히 붙는 시간
    [SerializeField] float superJumpResyncLerp = 8f;       // 붙는 속도(클수록 빠름)

    float superJumpResyncTimer = 0f;                       // 내부 타이머

    float releaseOrbitTimer = 0f;
    bool wasClinging = false;

    private Camera mainCamera;
    private float cameraPitch = 0f;
    private float yaw = 0f;
    private float burstShakeTimer = 0f;
    private float launchKickTimer = 0f;
    private float baseFov;
    private float perlinSeed;
    private float currentUpwardPitchT = 0f;
    private float currentDownwardPitchT = 0f;
    private bool isSwinging = false;
    private float currentSwingPivotOffsetX = 0f;
    private float targetSwingPivotOffsetX = 0f;

    void Awake()
    {
        mainCamera = GetComponent<Camera>();
        if (cameraTarget == null) Debug.LogError("Camera Target이 PlayerCameraController에 할당되지 않았습니다.");
        if (playerController == null) Debug.LogError("PlayerController가 PlayerCameraController에 할당되지 않았습니다.");
    }

    void Start()
    {
        currentCamDist = cameraDistance;
        baseFov = mainCamera ? mainCamera.fieldOfView : 60f;
        perlinSeed = Random.value * 1000f;
        if (cameraTarget) yaw = cameraTarget.eulerAngles.y;
    }

    void LateUpdate()
    {
        if (cameraTarget == null || playerController == null) return;

        // 홀딩 → 해제되는 프레임을 감지해서 타이머 시작
        bool justReleasedCling = wasClinging && !playerController.IsClinging;
        if (justReleasedCling && keepFreeOrbitAfterClingRelease)
            releaseOrbitTimer = releaseOrbitDuration;

        if (!playerController.WasHasteActive && playerController.IsHasteActive)
        {
            burstShakeTimer = 1f;
            launchKickTimer = launchKickTime;
        }

        ProcessLook();

        // 타이머 감소 및 상태 갱신
        if (releaseOrbitTimer > 0f) releaseOrbitTimer -= Time.deltaTime;
        wasClinging = playerController.IsClinging;
    }


    public void EnterSwingView() => isSwinging = true;
    public void ExitSwingView() { isSwinging = false; targetSwingPivotOffsetX = 0f; }

    void ProcessLook()
    {
        bool tumbling = playerController != null && playerController.IsTumbling;
        bool clinging = playerController != null && playerController.IsClinging;

        // 스윙 좌/우 피벗 오프셋 처리 (기존 그대로)
        if (isSwinging)
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            float stickX = gp != null ? gp.leftStick.x.ReadValue() : 0f;

            if ((kb != null && kb.aKey.isPressed) || stickX < -0.5f)
                targetSwingPivotOffsetX = -swingPivotOffsetXAmount;
            else if ((kb != null && kb.dKey.isPressed) || stickX > 0.5f)
                targetSwingPivotOffsetX = swingPivotOffsetXAmount;
            else
                targetSwingPivotOffsetX = 0f;
        }

        currentSwingPivotOffsetX = Mathf.Lerp(
            currentSwingPivotOffsetX,
            targetSwingPivotOffsetX,
            Time.deltaTime * swingPivotOffsetLerp
        );

        // ✅ 자유 오빗 조건에 "해제 직후 타이머"를 추가
        bool freeOrbit =
            (freeOrbitWhileTumbling && tumbling) ||
            isSwinging ||
            clinging ||
            (releaseOrbitTimer > 0f);

        if (freeOrbit)
        {
            yaw += playerController.LookInput.x * cameraRotationSpeed;
            cameraPitch -= playerController.LookInput.y * cameraRotationSpeed;
            cameraPitch = Mathf.Clamp(cameraPitch, minPitch, maxPitch);
        }
        else
        {
            // 평소엔 타겟 yaw에 동기화하되, 슈퍼점프 직후엔 부드럽게 따라가도록 보간
            float targetYaw = cameraTarget.eulerAngles.y;

            if (superJumpResyncTimer > 0f)
            {
                float k = 1f - Mathf.Exp(-superJumpResyncLerp * Time.deltaTime); // 지수 감쇠 보간
                yaw = Mathf.LerpAngle(yaw, targetYaw, k);
                superJumpResyncTimer -= Time.deltaTime;
            }
            else
            {
                yaw = targetYaw; // 평소처럼 즉시 동기화
            }

            cameraPitch -= playerController.LookInput.y * cameraRotationSpeed;
            cameraPitch = Mathf.Clamp(cameraPitch, minPitch, maxPitch);
        }


        // 이하 기존 그대로
        currentUpwardPitchT = 0f;
        currentDownwardPitchT = 0f;

        if (cameraPitch < 0) currentUpwardPitchT = Mathf.InverseLerp(0f, minPitch, cameraPitch);
        else if (cameraPitch > 0) currentDownwardPitchT = Mathf.InverseLerp(0f, maxPitch, cameraPitch);

        float dynamicHeight = Mathf.Lerp(0, maxPitchVerticalDisplacement, currentUpwardPitchT);
        Vector3 horizontalOffset = cameraTarget.right * currentSwingPivotOffsetX;
        Vector3 adjustedPivot = cameraTarget.position + pivotOffset - new Vector3(0, dynamicHeight, 0) + horizontalOffset;

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
                pivot,
                cameraProbeRadius,
                desiredDir,
                out RaycastHit hit,
                desiredDist,
                cameraCollisionMask,
                QueryTriggerInteraction.Ignore))
        {
            targetDist = Mathf.Max(minCameraDistance, hit.distance - cameraProbeRadius);
        }

        ApplyLaunchKick(ref targetDist);

        currentCamDist = Mathf.Lerp(
            currentCamDist,
            targetDist,
            1f - Mathf.Exp(-cameraCollisionLerp * Time.deltaTime)
        );

        transform.position = pivot + desiredDir * currentCamDist;
    }

    void ApplyFovEffect()
    {
        float hasteTargetFov = playerController.IsHasteActive ? hasteFov : baseFov;
        float upwardPitchTargetFov = Mathf.Lerp(baseFov, maxPitchFov, currentUpwardPitchT);
        float downwardPitchTargetFov = Mathf.Lerp(baseFov, maxDownwardPitchFov, currentDownwardPitchT);

        float defaultTargetFov = Mathf.Max(hasteTargetFov, upwardPitchTargetFov, downwardPitchTargetFov);
        float finalTargetFov = isSwinging ? swingFov : defaultTargetFov;

        mainCamera.fieldOfView = Mathf.Lerp(
            mainCamera.fieldOfView,
            finalTargetFov,
            1f - Mathf.Exp(-fovLerp * Time.deltaTime)
        );
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

    public void HoldFreeOrbit(float duration)
    {
        // 기존 releaseOrbitTimer를 재활용해 "자유 오빗" 상태를 강제로 유지
        releaseOrbitTimer = Mathf.Max(releaseOrbitTimer, duration);
    }

    // 슈퍼점프 정렬이 시작됐을 때 카메라 쪽 블렌드를 지시
    public void OnSuperJumpAlignStarted(float holdDuration = -1f, float resyncDuration = -1f)
    {
        if (holdDuration < 0f) holdDuration = superJumpFreeOrbitHold;
        if (resyncDuration < 0f) resyncDuration = superJumpResyncTime;

        // 잠깐 자유 오빗 유지(스냅 방지) — 기존 releaseOrbitTimer 재사용
        releaseOrbitTimer = Mathf.Max(releaseOrbitTimer, holdDuration);

        // 그 다음 일정 시간 동안은 타겟 Yaw로 서서히 보간
        superJumpResyncTimer = Mathf.Max(superJumpResyncTimer, resyncDuration);
    }

}
