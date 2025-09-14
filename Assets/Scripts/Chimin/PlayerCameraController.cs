using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCameraController : MonoBehaviour
{
    [Header("Target & References")]
    [Tooltip("카메라가 따라갈 목표입니다. 플레이어 본체가 아닌, 회전하지 않는 자식 오브젝트(CameraTarget)를 할당해야 합니다.")]
    [SerializeField] Transform cameraTarget; // 변수명을 playerTransform에서 cameraTarget으로 변경했습니다.
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
        if (cameraTarget == null)
            Debug.LogError("Camera Target이 PlayerCameraController에 할당되지 않았습니다.");
        if (playerController == null)
            Debug.LogError("PlayerController가 PlayerCameraController에 할당되지 않았습니다.");
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
        if (playerController.IsClinging) return;

        if (!playerController.WasHasteActive && playerController.IsHasteActive)
        {
            burstShakeTimer = 1f;
            launchKickTimer = launchKickTime;
        }

        ProcessLook();
    }

    // public void EnterSwingView()
    // {
    //     isSwinging = true;
    // }

    // public void ExitSwingView()
    // {
    //     isSwinging = false;
    //     targetSwingPivotOffsetX = 0f;
    // }

    void ProcessLook()
    {
        // 마우스 입력으로 yaw/pitch 누적 (항상 자유 시점)
        yaw += playerController.LookInput.x * cameraRotationSpeed;
        cameraPitch -= playerController.LookInput.y * cameraRotationSpeed;

        // 필요시 pitch 제한 (아래/위로 무한 회전 원하면 Clamp 제거)
        cameraPitch = Mathf.Clamp(cameraPitch, -89f, 89f);

        // 피벗 오프셋 등 기존 계산 유지
        currentSwingPivotOffsetX = Mathf.Lerp(
            currentSwingPivotOffsetX,
            targetSwingPivotOffsetX,
            Time.deltaTime * swingPivotOffsetLerp
        );

        currentUpwardPitchT = cameraPitch < 0f ? Mathf.InverseLerp(0f, -90f, cameraPitch) : 0f;
        currentDownwardPitchT = cameraPitch > 0f ? Mathf.InverseLerp(0f, 90f, cameraPitch) : 0f;

        float dynamicHeight = Mathf.Lerp(0, maxPitchVerticalDisplacement, currentUpwardPitchT);
        Vector3 horizontalOffset = cameraTarget.right * currentSwingPivotOffsetX;
        Vector3 adjustedPivot = cameraTarget.position + pivotOffset - new Vector3(0, dynamicHeight, 0) + horizontalOffset;

        float dynamicDistance = Mathf.Lerp(cameraDistance, minDistanceAtMaxPitch, currentUpwardPitchT);
        Quaternion camRot = Quaternion.Euler(cameraPitch, yaw, 0f);

        HandleCameraCollisionAndPositioning(adjustedPivot, camRot, dynamicDistance);
        transform.rotation = camRot; // LookAt 대신 직접 회전 적용

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
}
