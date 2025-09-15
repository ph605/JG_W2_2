using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float rotationSpeed = 0.1f;

    [Header("Haste (Charging)")]
    [SerializeField] float hasteHoldSeconds = 1.5f;
    [SerializeField] float hasteMultiplier = 3f;
    [SerializeField] InputActionReference hasteChargeAction;

    [Header("Haste UI")]
    [SerializeField] Image hasteFillImage;
    [SerializeField] TMP_Text hasteRemainText;

    [Header("Control Lock")]
    [SerializeField] bool controllerLocked = false;
    public void LockController() => controllerLocked = true;
    public void UnlockController() => controllerLocked = false;

    [Header("Events")]
    public UnityEvent onStageClear;
    public UnityEvent<GameObject> onCollectStar;
    public UnityEvent onDeathRequested;

    // PlayerController 클래스 상단 필드들 사이에 추가
    [Header("Super Jump")]
    [SerializeField] string superJumpLayerName = "SuperJump";
    [SerializeField] float superJumpUpVelocity = 18f;      // 위로 줄 목표 속도
    [SerializeField] float superJumpForwardBoost = 0f;     // 앞방향 추가 가속(원하면 사용)
    [SerializeField] bool cancelTumbleOnSuperJump = true;  // 점프 시 스핀 해제 여부


    [Header("Tumble Air Control")]
    [SerializeField] bool enableTumbleAirControl = true;
    [SerializeField] float tumbleAirAccel = 60f;     // 공중 가속(힘)
    [SerializeField] float tumbleAirMaxSpeed = 20f;  // 수평 최대 속도

    // PlayerController.cs
    [Header("View / Camera")]
    [SerializeField] Transform viewYawSource;   // 카메라 Transform 드롭(없으면 자동 할당)

    [Header("Super Jump Tuning")]
    [SerializeField, Range(5f, 60f)] float superJumpUprightLerpSpeed = 25f;


    int superJumpLayer;


    private bool canRotate = true; // 회전 가능 여부를 나타내는 플래그

    public void LockRotation() => canRotate = false;
    public void UnlockRotation() => canRotate = true;

    // CameraController가 참조할 플레이어 상태
    public bool IsHasteActive { get; private set; } = false;
    public bool IsHasteReady { get; private set; } = false;
    public float HasteHoldTimer { get; private set; } = 0f;
    public float HasteHoldDuration => hasteHoldSeconds;
    public bool WasHasteActive { get; private set; } = false;
    public bool IsClinging { get; set; }
    public bool IsTumbling { get; set; } = false;

    Rigidbody rb;
    Vector2 moveInput;

    // 마우스 입력을 다른 스크립트에서 읽을 수 있도록 public 속성으로 변경
    public Vector2 LookInput { get; private set; }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        superJumpLayer = LayerMask.NameToLayer(superJumpLayerName);

        if (viewYawSource == null && Camera.main != null)
            viewYawSource = Camera.main.transform;   // 자동
    }


    void OnEnable()
    {
        if (hasteChargeAction != null && hasteChargeAction.action != null)
            hasteChargeAction.action.Enable();
    }

    void OnDisable()
    {
        ResetHasteState();
        UpdateHasteUI(false);

        if (hasteChargeAction != null && hasteChargeAction.action != null)
            hasteChargeAction.action.Disable();
    }

    // 입력 이벤트 핸들러
    void OnMove(InputValue value) => moveInput = value.Get<Vector2>();
    void OnLook(InputValue value) => LookInput = value.Get<Vector2>();

    // 질주 키(CapsLock) 홀드 상태 확인
    bool IsHasteHolding()
    {
        if (hasteChargeAction != null && hasteChargeAction.action != null && hasteChargeAction.action.IsPressed())
            return true;

        var kb = Keyboard.current;
        return kb != null && kb.capsLockKey.isPressed;
    }

    void Update()
    {
        if (controllerLocked)
        {
            ResetHasteState();
            UpdateHasteUI(false);
            return;
        }

        ProcessHaste();
        ProcessPlayerRotation();
    }

    void FixedUpdate()
    {
        if (controllerLocked) return;
        ProcessMove();
    }

    void ProcessPlayerRotation()
    {
        if (!canRotate) return;

        float yawDelta = LookInput.x * rotationSpeed;

        if (IsTumbling)
        {
            // 스핀(X 회전)은 그대로 두고, 월드 Up 기준으로 Yaw만 추가
            transform.Rotate(Vector3.up, yawDelta, Space.World);
            return;
        }

        // 평상시 로직 (기존과 동일)
        float yaw = transform.eulerAngles.y + yawDelta;
        transform.eulerAngles = new Vector3(0f, yaw, 0f);
    }

    void ProcessHaste()
    {
        WasHasteActive = IsHasteActive; // 이전 프레임 상태 저장

        if (IsHasteActive)
        {
            HasteHoldTimer = hasteHoldSeconds;
            IsHasteReady = true;
            UpdateHasteUI(true);
        }
        else
        {
            bool holding = IsHasteHolding();

            if (holding)
            {
                HasteHoldTimer = Mathf.Min(HasteHoldTimer + Time.deltaTime, hasteHoldSeconds);
                IsHasteReady = HasteHoldTimer >= hasteHoldSeconds;

                if (IsHasteReady)
                    IsHasteActive = true;
            }
            else
            {
                HasteHoldTimer = 0f;
                IsHasteReady = false;
            }

            UpdateHasteUI(holding);
        }
    }

    void ProcessMove()
    {
        if (IsClinging) return;

        // ★ 스핀 중 공중 제어
        // ProcessMove()의 스핀 분기 안
        if (IsTumbling && enableTumbleAirControl)
        {
            Vector3 moveDir = GetMoveDirection(useCameraYaw: true); // ★ 카메라 기준
            if (moveDir.sqrMagnitude > 0.0001f)
                rb.AddForce(moveDir.normalized * tumbleAirAccel, ForceMode.Acceleration);

            Vector3 v = rb.linearVelocity;
            Vector2 hv = new Vector2(v.x, v.z);
            if (hv.magnitude > tumbleAirMaxSpeed)
            {
                hv = hv.normalized * tumbleAirMaxSpeed;
                rb.linearVelocity = new Vector3(hv.x, v.y, hv.y);
            }
            return;
        }


        // ↓ 기존 로직 그대로
        if (IsHasteActive)
        {
            Vector3 forward = transform.forward;
            Vector3 targetPos = rb.position + forward * (moveSpeed * hasteMultiplier) * Time.fixedDeltaTime;
            rb.MovePosition(targetPos);
            return;
        }

        Vector3 move = GetMoveDirection();
        if (IsHasteHolding() && !IsHasteReady) return;
        Vector3 target = rb.position + move * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(target);
    }


    // 기존 GetMoveDirection 교체
    public Vector3 GetMoveDirection(bool useCameraYaw = false)
    {
        Transform basis = (useCameraYaw && viewYawSource != null) ? viewYawSource : transform;

        Vector3 forward = basis.forward; forward.y = 0f;
        Vector3 right = basis.right; right.y = 0f;

        return right.normalized * moveInput.x + forward.normalized * moveInput.y;
    }


    void UpdateHasteUI(bool holding)
    {
        if (hasteFillImage)
        {
            float t = 0f;
            if (IsHasteActive) t = 1f;
            else if (holding) t = Mathf.Clamp01(HasteHoldTimer / hasteHoldSeconds);

            hasteFillImage.fillAmount = t;
        }

        if (hasteRemainText)
        {
            if (!IsHasteActive && holding && !IsHasteReady)
                hasteRemainText.text = (hasteHoldSeconds - HasteHoldTimer).ToString("0.0") + "s";
            else
                hasteRemainText.text = "";
        }
    }

    void ResetHasteState()
    {
        HasteHoldTimer = 0f;
        IsHasteReady = false;
        IsHasteActive = false;
        WasHasteActive = false;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Clear"))
            onStageClear?.Invoke();

        if (collision.gameObject.layer == superJumpLayer)
        {
            // ★ 스핀 강제 종료 + 카메라 Yaw로 자연스럽게 세우기
            var grapple = GetComponent<PlayerGrapple>();
            if (grapple != null)
                grapple.StopTumbleForSuperJump(viewYawSource, superJumpUprightLerpSpeed); // 속도는 취향대로

            DoSuperJump(); // 그 다음 위로 튕기기
        }
    }


    void DoSuperJump()
    {
        // 위쪽 속도를 최소 superJumpUpVelocity 이상으로 설정
        var v = rb.linearVelocity;
        v.y = Mathf.Max(v.y, superJumpUpVelocity);

        // 앞으로 살짝 밀고 싶으면 옵션 사용
        if (superJumpForwardBoost > 0f)
            v += transform.forward * superJumpForwardBoost;

        rb.linearVelocity = v;

        // 스핀/조작 상태 정리(원하면)
        if (cancelTumbleOnSuperJump)
            IsTumbling = false;

        // 필요하면 여기서 추가 이펙트/사운드 트리거 가능
        // Debug.Log("[Player] SuperJump triggered");
    }


}
