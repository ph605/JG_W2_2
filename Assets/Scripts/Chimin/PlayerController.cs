using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;   // UI Image.fillAmount
using TMPro;            // 남은 시간 텍스트 표시
using UnityEngine.Events;   // 이벤트용

public class PlayerController : MonoBehaviour
{
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float rotationSpeed = 0.1f;
    [SerializeField] float cameraRotationSpeed = 0.05f;
    [SerializeField] float minPitch = -30f;
    [SerializeField] float maxPitch = 60f;
    [SerializeField] float cameraDistance = 10f;

    [SerializeField] LayerMask cameraCollisionMask = ~0;
    [SerializeField] float minCameraDistance = 0.3f;
    [SerializeField] float cameraProbeRadius = 0.2f;
    [SerializeField] float cameraCollisionLerp = 20f;
    float currentCamDist;

    [SerializeField] ParticleSystem starParticlePrefab;

    // ─ Haste(차징) ─
    [SerializeField] float hasteHoldSeconds = 1.5f;   // CAPS LOCK 유지 시 차징 시간
    [SerializeField] float hasteMultiplier = 3f;      // 차징 완료 후 속도 배수
    bool isHasteReady = false;                        // 차징 완료 여부
    float capsHoldTimer = 0f;                         // CAPS LOCK 누른 시간 누적
    [SerializeField] InputActionReference hasteChargeAction; // 선택: 액션으로도 홀드 감지
    [Header("Haste UI")]
    [SerializeField] Image hasteFillImage;            // 차징 바
    [SerializeField] TMP_Text hasteRemainText;        // 남은 시간 텍스트
    bool isHasteActive = false;                       // 차징 완료 후 전방 질주 상태(멈출 수 없음)

    // ─ Camera FX ─
    [Header("Camera FX")]
    [SerializeField] float chargeShakeMax = 0.15f;    // 차징 중 최대 흔들림
    [SerializeField] float chargeShakeFreq = 18f;     // 차징 흔들림 주파수
    [SerializeField] float speedShakeIntensity = 0.12f; // 질주 중 지속 흔들림
    [SerializeField] float speedShakeFreq = 22f;      // 질주 흔들림 주파수
    [SerializeField] float burstShakeIntensity = 0.35f; // 출발 임펄스 세기
    [SerializeField] float burstShakeDecay = 4f;      // 임펄스 감쇠 속도
    [SerializeField] float posJitter = 0.05f;         // 카메라 위치 흔들림 범위
    [SerializeField] float rotJitter = 1.5f;          // 카메라 회전 흔들림 범위(도)
    [SerializeField] float launchKickBack = 0.6f;     // 출발 순간 뒤로 킥백 거리
    [SerializeField] float launchKickTime = 0.15f;    // 킥백 유지 시간
    [SerializeField] float hasteFov = 72f;            // 질주 중 FOV
    [SerializeField] float fovLerp = 6f;              // FOV 보간 강도

    [Header("Control Lock (no GameManager)")]
    [SerializeField] bool controllerLocked = false;     // 필요 시 외부에서 이동/입력 잠그기
    public void LockController() => controllerLocked = true;
    public void UnlockController() => controllerLocked = false;

    [Header("Events (replace GameManager calls)")]
    public UnityEvent onStageClear;                     // 클리어 시 발생
    public UnityEvent<GameObject> onCollectStar;        // 아이템 먹을 때 전달
    public UnityEvent onDeathRequested;                 // 사망/리스폰 요청

    float burstShakeTimer = 0f;
    float launchKickTimer = 0f;
    bool wasHasteActive = false;
    float baseFov;
    float perlinSeed;

    public Camera playerCamera;

    Rigidbody rb;
    Vector2 moveInput;
    Vector2 lookInput;
    float cameraPitch = 0f;

    public bool IsClinging { get; set; }

    Player playerCore; // Player 컴포넌트 캐시

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerCore = GetComponent<Player>();
    }

    void OnEnable()
    {
        if (hasteChargeAction != null && hasteChargeAction.action != null)
            hasteChargeAction.action.Enable();
    }

    void Start()
    {

        currentCamDist = cameraDistance;

        baseFov = playerCamera ? playerCamera.fieldOfView : 60f;
        perlinSeed = Random.value * 1000f;
    }

    void OnMove(InputValue value) => moveInput = value.Get<Vector2>();
    void OnLook(InputValue value) => lookInput = value.Get<Vector2>();

    bool IsHasteHolding()
    {
        if (hasteChargeAction != null && hasteChargeAction.action != null && hasteChargeAction.action.IsPressed())
            return true;

        var kb = Keyboard.current;
        return kb != null && kb.capsLockKey.isPressed;
    }

    void ProcessLook()
    {
        float yaw = transform.eulerAngles.y + lookInput.x * rotationSpeed;
        cameraPitch -= lookInput.y * cameraRotationSpeed;
        cameraPitch = Mathf.Clamp(cameraPitch, minPitch, maxPitch);
        transform.eulerAngles = new Vector3(0f, yaw, 0f);

        Quaternion camRot = Quaternion.Euler(cameraPitch, yaw, 0f);
        Vector3 pivot = transform.position + Vector3.up * 1.5f;

        Vector3 desiredDir = camRot * Vector3.back;
        float desiredDist = cameraDistance;

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

        // 출발 킥백(카메라 거리 보정)을 거리 보간 전에 적용
        ApplyLaunchKick(ref targetDist);

        currentCamDist = Mathf.Lerp(
            currentCamDist,
            targetDist,
            1f - Mathf.Exp(-cameraCollisionLerp * Time.deltaTime));

        Vector3 camPos = pivot + desiredDir * currentCamDist;
        playerCamera.transform.position = camPos;
        playerCamera.transform.LookAt(pivot);

        // 질주 중 FOV 확대
        float targetFov = isHasteActive ? hasteFov : baseFov;
        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFov,
            1f - Mathf.Exp(-fovLerp * Time.deltaTime)
        );

        // 마지막에 흔들림 적용(위치/회전 노이즈)
        ApplyCameraShake();
    }

    void UpdateHasteUI(bool holding)
    {
        if (hasteFillImage)
        {
            float t = 0f;
            if (isHasteActive) t = 1f; // 질주 중엔 항상 가득
            else if (holding) t = Mathf.Clamp01(capsHoldTimer / hasteHoldSeconds);
            hasteFillImage.fillAmount = t;
        }

        if (hasteRemainText)
        {
            if (!isHasteActive && holding && !isHasteReady)
                hasteRemainText.text = (hasteHoldSeconds - capsHoldTimer).ToString("0.0") + "s";
            else
                hasteRemainText.text = "";
        }
    }

    void FixedUpdate()
    {
        if (controllerLocked) return;
        ProcessMove();
    }

    void LateUpdate()
    {
        if (controllerLocked) return;
        ProcessLook();
    }

    void Update()
    {
        if (controllerLocked)
        {
            capsHoldTimer = 0f;
            isHasteReady = false;
            isHasteActive = false;
            burstShakeTimer = 0f;
            launchKickTimer = 0f;
            wasHasteActive = false;
            if (hasteFillImage) hasteFillImage.fillAmount = 0f;
            if (hasteRemainText) hasteRemainText.text = "";
            return;
        }

        // 질주가 이미 시작되었으면 유지(멈출 수 없음)
        if (isHasteActive)
        {
            capsHoldTimer = hasteHoldSeconds;
            isHasteReady = true;
            UpdateHasteUI(true);
        }
        else
        {
            // 아직 질주 전: 차징 진행
            bool holding = IsHasteHolding();
            if (holding)
            {
                capsHoldTimer = Mathf.Min(capsHoldTimer + Time.deltaTime, hasteHoldSeconds);
                isHasteReady = capsHoldTimer >= hasteHoldSeconds;

                // 차징 완료 순간 질주 상태 래치
                if (isHasteReady)
                    isHasteActive = true;
            }
            else
            {
                capsHoldTimer = 0f;
                isHasteReady = false;
            }

            UpdateHasteUI(holding);
        }

        // 출발 순간 임펄스/킥백 트리거
        if (!wasHasteActive && isHasteActive)
        {
            burstShakeTimer = 1f;             // 임펄스 시작
            launchKickTimer = launchKickTime;  // 킥백 시작
        }
        wasHasteActive = isHasteActive;
    }

    public Vector3 GetMoveDirection()
    {
        Vector3 camForward = playerCamera.transform.forward;
        Vector3 camRight = playerCamera.transform.right;
        camForward.y = 0;
        camRight.y = 0;
        camForward.Normalize();
        camRight.Normalize();
        return camRight * moveInput.x + camForward * moveInput.y;
    }

    void ProcessMove()
    {
        if (IsClinging) return;

        // 질주 중: 무조건 전방으로 3배속(입력 무시), 마우스 회전으로만 방향 전환
        if (isHasteActive)
        {
            Vector3 forward = transform.forward;
            Vector3 targetPos = rb.position + forward * (moveSpeed * hasteMultiplier) * Time.fixedDeltaTime;
            rb.MovePosition(targetPos);
            return;
        }

        // 평상시 이동
        Vector3 moveDir = GetMoveDirection();

        // 차징 중(아직 준비 전)엔 정지
        bool holding = IsHasteHolding();
        if (holding && !isHasteReady)
            return;

        float speed = moveSpeed * ((holding && isHasteReady) ? hasteMultiplier : 1f);
        Vector3 target = rb.position + moveDir * speed * Time.fixedDeltaTime;
        rb.MovePosition(target);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Clear"))
        {
            onStageClear?.Invoke();   // 외부(UI/매니저)가 이 이벤트 받아서 처리
        }
    }






    void OnDisable()
    {
        capsHoldTimer = 0f;
        isHasteReady = false;
        isHasteActive = false;
        burstShakeTimer = 0f;
        launchKickTimer = 0f;
        wasHasteActive = false;

        if (hasteFillImage) hasteFillImage.fillAmount = 0f;
        if (hasteRemainText) hasteRemainText.text = "";
        if (playerCamera) playerCamera.fieldOfView = baseFov;

        if (hasteChargeAction != null && hasteChargeAction.action != null)
            hasteChargeAction.action.Disable();
    }

    // ─ Camera FX Helpers ─
    void ApplyLaunchKick(ref float targetDist)
    {
        if (launchKickTimer <= 0f) return;
        float k = Mathf.Clamp01(launchKickTimer / launchKickTime); // 1→0
        targetDist += launchKickBack * k;
        launchKickTimer = Mathf.Max(0f, launchKickTimer - Time.deltaTime);
    }

    void ApplyCameraShake()
    {
        float amp = 0f;
        float t = Time.time;

        // 차징 흔들림(차징 진행도 비례)
        bool holding = IsHasteHolding();
        if (holding && !isHasteActive)
        {
            float chargeT = Mathf.Clamp01(capsHoldTimer / hasteHoldSeconds);
            amp += chargeShakeMax * chargeT;
        }

        // 출발 임펄스(감쇠)
        if (burstShakeTimer > 0f)
        {
            amp += burstShakeIntensity * burstShakeTimer;
            burstShakeTimer = Mathf.MoveTowards(burstShakeTimer, 0f, burstShakeDecay * Time.deltaTime);
        }

        // 질주 지속 흔들림
        if (isHasteActive) amp += speedShakeIntensity;

        if (amp <= 0f) return;

        float nx = (Mathf.PerlinNoise(perlinSeed + t * chargeShakeFreq, 0f) - 0.5f) * 2f;
        float ny = (Mathf.PerlinNoise(perlinSeed + 100f + t * speedShakeFreq, 0f) - 0.5f) * 2f;

        Vector3 posOffset = new Vector3(nx, ny, 0f) * posJitter * amp;
        Vector3 rotOffset = new Vector3(ny, 0f, -nx) * rotJitter * amp;

        playerCamera.transform.position += posOffset;
        playerCamera.transform.rotation = Quaternion.Euler(playerCamera.transform.eulerAngles + rotOffset);
    }
}
