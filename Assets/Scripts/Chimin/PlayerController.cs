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

    int superJumpLayer;


    private bool canRotate = true; // 회전 가능 여부를 나타내는 플래그
    // ... (다른 함수들 아래에 이 두 함수를 추가)
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
        if (IsTumbling) return; // << 이 줄을 추가하세요
        if (IsClinging) return;

        if (IsHasteActive)
        {
            Vector3 forward = transform.forward;
            Vector3 targetPos = rb.position + forward * (moveSpeed * hasteMultiplier) * Time.fixedDeltaTime;
            rb.MovePosition(targetPos);
            return;
        }

        Vector3 moveDir = GetMoveDirection();

        if (IsHasteHolding() && !IsHasteReady)
            return;

        Vector3 target = rb.position + moveDir * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(target);
    }

    public Vector3 GetMoveDirection()
    {
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        forward.y = 0;
        right.y = 0;
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
        {
            onStageClear?.Invoke();
        }

        // ↓ 추가: SuperJump 레이어 밟으면 위로 튀기
        if (collision.gameObject.layer == superJumpLayer)
        {
            DoSuperJump();
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
