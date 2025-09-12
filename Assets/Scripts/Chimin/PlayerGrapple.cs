using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerGrapple : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Camera cam;                  // 레이 기준 카메라
    [SerializeField] LineRenderer rope;           // 로프 시각화
    [SerializeField] PlayerInput playerInput;     // PlayerInput (없으면 동일 오브젝트에서 자동 획득)
    [SerializeField] GameObject aim;              // 에이밍 표시 오브젝트(선택)

    [Header("Aim/Raycast")]
    [SerializeField] LayerMask layerMask = ~0;    // 훅 가능 표면 레이어(조준 탐색)
    [SerializeField] float rayDistance = 60f;     // 에이밍 탐색 거리
    [SerializeField] float sphereRadius = 3f;     // 스피어캐스트 반경(레이 실패 보정)

    [Header("Spring Joint (Swing)")]
    [SerializeField] float springForce = 35f;     // sj.spring
    [SerializeField] float springDamper = 4f;     // sj.damper
    [SerializeField] float springMass = 1f;       // sj.massScale
    [SerializeField, Range(0.05f, 0.95f)] float minDistFrac = 0.2f; // sj.minDistance = dis * minDistFrac
    [SerializeField, Range(0.05f, 0.95f)] float maxDistFrac = 0.8f; // sj.maxDistance = dis * maxDistFrac
    [SerializeField] float maxSwingSpeed = 40f;   // 스윙 중 최대 선형 속도 캡

    [Header("Extra Push (optional)")]
    [SerializeField] bool enableWPush = true;     // W키 가속 사용 여부
    [SerializeField] float pushForwardTop = 10f;
    [SerializeField] float pushForwardSide = 15f;
    [SerializeField] float pushUpSide = 5f;

    [Header("Wall Swing (optional)")]
    [SerializeField] bool enableWallPush = true;    // 옆벽 스윙 가속 사용 여부
    [SerializeField] float pushSideways = 20f;      // 옆벽 스윙 시 측면으로 미는 힘

    // 상태
    Rigidbody rb;
    SpringJoint sj;
    bool isSwing = false;
    Vector3 anchor;                  // 훅 지점
    RaycastHit lastHit;              // 마지막 유효 히트(법선/태그 참조용)
    int currentLayer = -1;           // ← 레이어 저장(추가)

    // 입력
    InputAction grappleAction;       // "Grapple"
    Vector3 lastMousePos;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (!playerInput) playerInput = GetComponent<PlayerInput>();

        if (playerInput != null)
        {
            grappleAction = playerInput.actions["Grapple"];
            if (grappleAction != null)
            {
                grappleAction.started += OnGrappleStarted;
                grappleAction.canceled += OnGrappleCanceled;
            }
        }

        if (rope)
        {
            rope.positionCount = 0;
            rope.useWorldSpace = true;
            rope.enabled = false;
        }
    }

    void OnDestroy()
    {
        if (grappleAction != null)
        {
            grappleAction.started -= OnGrappleStarted;
            grappleAction.canceled -= OnGrappleCanceled;
        }
    }

    void Update()
    {
        // 에이밍(훅 포인트 갱신)
        UpdateHookPoint();

        // 시각화
        DrawRope();

        lastMousePos = Input.mousePosition;
    }

    void FixedUpdate()
    {
        if (!isSwing) return;

        // 최대 속도 캡
        if (rb.linearVelocity.sqrMagnitude > maxSwingSpeed * maxSwingSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSwingSpeed;

        // W키 추진력 (레이어 기반)
        if (enableWPush)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.wKey.isPressed)
            {
                if (currentLayer == LayerMask.NameToLayer("Top"))
                {
                    rb.AddForce(cam.transform.forward * pushForwardTop, ForceMode.Force);
                }
                else if (currentLayer == LayerMask.NameToLayer("Left") ||
                         currentLayer == LayerMask.NameToLayer("Right"))
                {
                    rb.AddForce(cam.transform.forward * pushForwardSide, ForceMode.Force);
                    rb.AddForce(Vector3.up * pushUpSide, ForceMode.Force);
                }
            }
        }

        // 옆벽 스윙 시 자동으로 측면 힘 추가
        if (enableWallPush)
        {
            // ★★★ 방향 수정 ★★★
            if (currentLayer == LayerMask.NameToLayer("Left"))
            {
                // 왼쪽 벽에 붙었을 때 (벽 반대편인) 오른쪽(+X)으로 힘을 줌
                rb.AddForce(Vector3.right * pushSideways, ForceMode.Force);
            }
            else if (currentLayer == LayerMask.NameToLayer("Right"))
            {
                // 오른쪽 벽에 붙었을 때 (벽 반대편인) 왼쪽(-X)으로 힘을 줌
                rb.AddForce(Vector3.left * pushSideways, ForceMode.Force);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 입력 콜백
    void OnGrappleStarted(InputAction.CallbackContext ctx)
    {
        TryStartSwing();
        lastMousePos = Input.mousePosition;
    }
    void OnGrappleCanceled(InputAction.CallbackContext ctx)
    {
        EndSwing();
    }

    // ─────────────────────────────────────────────────────────────
    // 에이밍(레이 + 스피어캐스트) → lastHit/aim 갱신
    void UpdateHookPoint()
    {
        if (!cam) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        bool got = Physics.Raycast(ray, out RaycastHit hit, rayDistance, layerMask, QueryTriggerInteraction.Collide);
        if (!got)
        {
            // 레이가 빗나가면 스피어캐스트로 보정
            got = Physics.SphereCast(cam.transform.position, sphereRadius, cam.transform.forward,
                                     out hit, rayDistance, layerMask, QueryTriggerInteraction.Collide);
        }

        if (got)
        {
            lastHit = hit;
            if (aim && !aim.activeSelf) aim.SetActive(true);
            if (aim) aim.transform.position = hit.point;
        }
        else
        {
            lastHit = new RaycastHit(); // 무효
            if (aim && aim.activeSelf) aim.SetActive(false); // ← 숨김
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 스윙 시작
    void TryStartSwing()
    {
        if (isSwing) return;
        if (lastHit.point == Vector3.zero) return;

        isSwing = true;

        anchor = lastHit.point;
        currentLayer = lastHit.collider ? lastHit.collider.gameObject.layer : -1; // ★ 레이어 저장


        // 라인렌더러 준비
        if (rope)
        {
            rope.enabled = true;
            rope.positionCount = 2;
            rope.SetPosition(0, transform.position);
            rope.SetPosition(1, anchor);
        }

        // 스프링 조인트 생성
        if (!sj) sj = gameObject.AddComponent<SpringJoint>();
        sj.autoConfigureConnectedAnchor = false;
        sj.connectedAnchor = anchor;

        sj.spring = springForce;
        sj.damper = springDamper;
        sj.massScale = springMass;

        float dis = Vector3.Distance(transform.position, anchor);
        sj.maxDistance = Mathf.Max(0.01f, dis * maxDistFrac);
        sj.minDistance = Mathf.Clamp(dis * minDistFrac, 0f, sj.maxDistance);

        // 필요 시 약간의 감쇠를 위해 선형 댐핑을 낮추거나 조정 가능
        // rb.linearDamping = ...
    }

    // 스윙 종료
    void EndSwing()
    {
        if (!isSwing) return;

        // 살짝 감쇠해 튀는 느낌 감소
        rb.linearVelocity = new Vector3(
            rb.linearVelocity.x * 0.5f,
            rb.linearVelocity.y * 0.8f,
            rb.linearVelocity.z
        );

        isSwing = false;

        if (rope)
        {
            rope.positionCount = 0;
            rope.enabled = false;
        }

        if (sj) Destroy(sj);
        sj = null;
    }

    // ─────────────────────────────────────────────────────────────
    // 로프 시각화
    void DrawRope()
    {
        if (!isSwing || !rope) return;
        rope.SetPosition(0, transform.position);
        rope.SetPosition(1, anchor);
    }
}
