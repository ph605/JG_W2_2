using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System;

public class PlayerGrapple : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Camera cam;
    [SerializeField] LineRenderer rope;
    [SerializeField] PlayerInput playerInput;
    [SerializeField] GameObject aim;
    [SerializeField] PlayerCameraController cameraController;
    [SerializeField] PlayerController playerController;
    [SerializeField] RotationTracker rotationTracker;
    [SerializeField] CameraRigFollow cameraRig; // ← CameraRig 드롭
    ConfigurableJoint cj;

    [Header("Aim/Raycast")]
    [SerializeField] LayerMask layerMask;
    // [SerializeField] float rayDistance = 60f;
    // [SerializeField] float sphereRadius = 3f;

    // [Header("Spring Joint (Swing)")]
    // [SerializeField] float springForce = 35f;
    // [SerializeField] float springDamper = 4f;
    // [SerializeField] float springMass = 1f;
    // [SerializeField, Range(0.05f, 0.95f)] float minDistFrac = 0.2f;
    // [SerializeField, Range(0.05f, 0.95f)] float maxDistFrac = 0.8f;
    // [SerializeField] float maxSwingSpeed = 40f;

    // [Header("Extra Push (optional)")]
    // [SerializeField] bool enableWPush = true;
    // [SerializeField] float pushForwardTop = 10f;
    // [SerializeField] float pushForwardSide = 15f;
    // [SerializeField] float pushUpSide = 5f;

    [Header("Wall Swing (optional)")]
    [SerializeField] bool enableWallPush = true;
    [SerializeField] float pushSideways = 20f;

    [Header("Release Physics")]
    [SerializeField] float releaseSpinMultiplier = 0.5f;
    [SerializeField] float settleSpeed = 8f;

    [Header("Swing Tilting")]
    [SerializeField] float tiltSpeed = 5f;

    [Header("Spin Correction")]
    [SerializeField] float almostFullTurnThreshold = 300f; // 270~330

    [Header("Landing")]
    [SerializeField] bool alwaysAlignOnLanding = true; // ← 착지 시 항상 카메라 방향으로 맞추기
    Vector3 spot;
    [Header("SphereCast Settings")]
    public float radius;      // 구체 반지름
    RaycastHit hit;
    [Header("Hang")]
    public float hangingTime;
    private float currentHangTime = 0;
    [Header("Raycast")]
    public float maxRayDistance;
    public float maxRopeDistance;
    public float ropeDistance;
    public float minDistance;
    public float growSpeed; 
    // 상태
    Rigidbody rb;
    SpringJoint sj;
    bool isSwing = false;
    // Vector3 anchor;
    // RaycastHit lastHit;
    private float directionCheck = 0f;

    // 입력
    InputAction grappleAction;
    Vector3 lastMousePos;

    // 코루틴 상태
    private Coroutine tumbleCoroutine;
    // 착지 후 회전 복구 중인지
    private bool isSettling = false;
    //상운──────────────────────────────────────────────────

    void Awake()
    {
        //리지드 바디 로드겸 각 속도 무한 세팅
        rb = GetComponent<Rigidbody>();
        rb.maxAngularVelocity = Mathf.Infinity;

        if (!playerInput) playerInput = GetComponent<PlayerInput>();
        if (!playerController) playerController = GetComponent<PlayerController>();

        //입력 액션 연결 *추후 공부 필요
        // if (playerInput != null)
        // {
        //     grappleAction = playerInput.actions["Grapple"];
        //     if (grappleAction != null)
        //     {
        //         grappleAction.started += OnGrappleStarted;
        //         grappleAction.canceled += OnGrappleCanceled;
        //     }
        // }

        //로프 초기화
        if (rope)
        {
            rope.positionCount = 0;
            // rope.useWorldSpace = true;
            // rope.enabled = false;
        }

        if (!rotationTracker)
        {
            rotationTracker = GetComponent<RotationTracker>();
            if (!rotationTracker) rotationTracker = GetComponentInChildren<RotationTracker>(true);
        }
    }

    // void OnDestroy()
    // {
    //     if (grappleAction != null)
    //     {
    //         grappleAction.started -= OnGrappleStarted;
    //         grappleAction.canceled -= OnGrappleCanceled;
    //     }
    // }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            JumpStart();
        }
        if (isSwing)
        {
            rb.AddForce(Vector3.down * 5f, ForceMode.Acceleration);
            currentHangTime += Time.deltaTime;
            float t = currentHangTime / hangingTime;
            float currentWidth = Mathf.Lerp(0.1f, 0f, t);
            rope.startWidth = currentWidth;
            rope.endWidth = currentWidth;
            if (currentHangTime >= hangingTime)
            {
                EndSwing();
                currentHangTime = 0f;
                rope.startWidth = 0.1f;
            }
            if (ropeDistance >= maxRopeDistance)
            {
                ropeDistance -= Time.deltaTime * growSpeed;
                if (ropeDistance <= maxRopeDistance)
                {
                    ropeDistance = maxRopeDistance;
                }
                SoftJointLimit limit = new SoftJointLimit();
                limit.limit = ropeDistance;
                cj.linearLimit = limit;
            }
            if (ropeDistance < minDistance)
            {
                ropeDistance += Time.deltaTime * growSpeed;
                if (ropeDistance >= minDistance)
                {
                    ropeDistance = minDistance;
                }
                SoftJointLimit limit = new SoftJointLimit();
                limit.limit = ropeDistance;
                cj.linearLimit = limit;
            }
        }
        else
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Physics.Raycast(ray, out hit, maxRayDistance, layerMask);
            aim.transform.position = ray.GetPoint(maxRayDistance);
            rb.maxLinearVelocity = 40f;
        }

        //마우스
        if (Input.GetMouseButtonDown(0))
        {
            if (isSwing)
            {
                // rb.AddForce(Vector3.forward * 10f, ForceMode.Acceleration);
            }
            else
            {
                HookPoint();
                TryStartSwing();
            }
        }
        else if (Input.GetMouseButton(0))
        {
            if (isSwing)
            {
                rb.AddForce(transform.forward * 20f, ForceMode.Acceleration);
                rb.AddForce(transform.up * -5f, ForceMode.Acceleration);
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            if (isSwing)
            {
                EndSwing();
            }
        }
        DrawRope();
        lastMousePos = Input.mousePosition; // 매 프레임 갱신
    }

    void FixedUpdate()
    {
        if (!isSwing) return;

        // if (rb.linearVelocity.sqrMagnitude > maxSwingSpeed * maxSwingSpeed)
        //     rb.linearVelocity = rb.linearVelocity.normalized * maxSwingSpeed;

        // if (enableWPush)
        // {
        //     var kb = Keyboard.current;
        //     if (kb != null && kb.wKey.isPressed)
        //     {
        //         if (currentLayer == LayerMask.NameToLayer("Left"))
        //             rb.AddForce(transform.right * pushSideways, ForceMode.Force);
        //         else if (currentLayer == LayerMask.NameToLayer("Right"))
        //             rb.AddForce(-transform.right * pushSideways, ForceMode.Force);
        //     }
        // }
        // if (enableWallPush)
        // {
        //     if (currentLayer == LayerMask.NameToLayer("Left"))
        //         rb.AddForce(Vector3.right * pushSideways, ForceMode.Force);
        //     else if (currentLayer == LayerMask.NameToLayer("Right"))
        //         rb.AddForce(Vector3.left * pushSideways, ForceMode.Force);
        // }
        //캐릭터가 움직이기 시작
        if (rb.linearVelocity.sqrMagnitude > 0.1f)
        {
            Vector3 ropeDirection = (spot - transform.position).normalized;
            Vector3 playerForward = rb.linearVelocity.normalized;
            Quaternion targetRotation = Quaternion.LookRotation(playerForward, ropeDirection);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * tiltSpeed));
        }
    }

    // 입력
    // void OnGrappleStarted(InputAction.CallbackContext ctx)
    // {
    //     TryStartSwing();
    //     lastMousePos = Input.mousePosition;
    // }

    // void OnGrappleCanceled(InputAction.CallbackContext ctx)
    // {
    //     EndSwing();
    // }

    void HookPoint()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        // SphereCast로 충돌체 검사
        if (Physics.SphereCast(ray, radius, out hit, maxRayDistance, layerMask))
        {
            Debug.Log("Test");
            Debug.Log(hit.transform.name);
            aim.transform.position = hit.point;
        }
    }
    // 에이밍
    // void UpdateHookPoint()
    // {
    //     if (!cam) return;

    //     Ray ray = cam.ScreenPointToRay(Input.mousePosition);
    //     bool got = Physics.Raycast(ray, out RaycastHit hit, rayDistance, layerMask, QueryTriggerInteraction.Collide);

    //     if (!got)
    //     {
    //         got = Physics.SphereCast(
    //             cam.transform.position,
    //             sphereRadius,
    //             cam.transform.forward,
    //             out hit,
    //             rayDistance,
    //             layerMask,
    //             QueryTriggerInteraction.Collide
    //         );
    //     }

    //     if (got)
    //     {
    //         lastHit = hit;
    //         if (aim && !aim.activeSelf) aim.SetActive(true);
    //         if (aim) aim.transform.position = hit.point;
    //     }
    //     else
    //     {
    //         lastHit = new RaycastHit();
    //         if (aim && aim.activeSelf) aim.SetActive(false);
    //     }
    // }

    // 스윙 시작
    void TryStartSwing()
    {
        //회전 코루틴 무조건 실행
        if (tumbleCoroutine != null)
        {
            StopCoroutine(tumbleCoroutine);
            isSettling = false;
            rb.isKinematic = false;

            // (1) 회전 카운트 중단 (스윙 중 틸트가 스핀으로 카운트되는 것 방지)
            rotationTracker?.StopTracking();

            // (2) 아직 카메라가 고정돼 있을 때 플레이어 Yaw를 '현재 카메라 시선'으로 스냅
            SnapPlayerYawToCameraView();

            // (3) 남아있는 각속도 제거 (재스윙 직후 흔들림 방지)
            rb.angularVelocity = Vector3.zero;

            // (4) 이제 스핀 종료 처리
            if (playerController != null)
            {
                playerController.IsTumbling = false;
                // playerController.UnlockController();
                // playerController.UnlockRotation();
            }
        }

        if (isSwing) return;
        if (hit.point == Vector3.zero) return;

        isSwing = true;
        spot = hit.point;

        // --- 변경: 스윙 시작 시 플레이어를 히트 위치 쪽으로 회전(수평 Yaw만)
        // Vector3 lookDir = spot - transform.position;
        // lookDir.y = 0f;
        // if (lookDir.sqrMagnitude > 1e-6f)
        // {
        //     float yaw = Mathf.Atan2(lookDir.x, lookDir.z) * Mathf.Rad2Deg;
        //     Quaternion targetRot = Quaternion.Euler(0f, yaw, 0f);

        //     // 즉시 적용 (transform + Rigidbody 둘다 설정)
        //     transform.rotation = targetRot;
        //     if (rb != null)
        //         rb.MoveRotation(targetRot);
        // }
        // --- 변경 끝
        // rb.constraints = RigidbodyConstraints.FreezeRotation;
        // currentLayer = hit.collider ? hit.collider.gameObject.layer : -1;

        // if (playerController != null) playerController.LockRotation();
        // if (cameraController != null) cameraController.EnterSwingView();

        //로프 시각화
        // if (rope)
        // {
        //     rope.enabled = true;
        rope.positionCount = 2;
        rope.SetPosition(0, transform.position);
        rope.SetPosition(1, spot);
        ropeDistance = Vector3.Distance(spot, transform.position);
        // }
        //스프링 조인트 *추후 configurable Joint로 변경 필요
        // if (!sj) sj = gameObject.AddComponent<SpringJoint>();
        // sj.autoConfigureConnectedAnchor = false;
        // sj.connectedAnchor = anchor;
        // sj.spring = springForce;
        // sj.damper = springDamper;
        // sj.massScale = springMass;

        // float dis = Vector3.Distance(transform.position, anchor);
        // sj.maxRayDistance = Mathf.Max(0.01f, dis * maxDistFrac);
        // sj.minDistance = Mathf.Clamp(dis * minDistFrac, 0f, sj.maxRayDistance);

        cj = gameObject.AddComponent<ConfigurableJoint>();
        cj.connectedAnchor = spot; // 줄이 고정된 지점
        cj.autoConfigureConnectedAnchor = false;
        // 이동 제약
        cj.xMotion = ConfigurableJointMotion.Limited;
        cj.yMotion = ConfigurableJointMotion.Limited;
        cj.zMotion = ConfigurableJointMotion.Limited;
        // 회전은 자유
        cj.angularXMotion = ConfigurableJointMotion.Free;
        cj.angularYMotion = ConfigurableJointMotion.Free;
        cj.angularZMotion = ConfigurableJointMotion.Free;

        // 줄 최대 길이
        SoftJointLimit limit = new SoftJointLimit();
        if (Vector3.Distance(spot, transform.position) < minDistance)
        {
            limit.limit = minDistance;
        }
        else
        {
            limit.limit = Vector3.Distance(spot, transform.position);
        }
        cj.linearLimit = limit;
        // 줄을 탄탄하게 (스프링 효과 X)
        JointDrive drive = new JointDrive();
        drive.positionSpring = 0f;
        drive.positionDamper = 0f;
        drive.maximumForce = Mathf.Infinity;
        cj.xDrive = cj.yDrive = cj.zDrive = drive;
    }

    // 스윙 종료
    void EndSwing()
    {
        if (!isSwing) return;

        isSwing = false;
        // if (cameraController != null) cameraController.ExitSwingView();
        //코루틴 강제 종료
        if (tumbleCoroutine != null) StopCoroutine(tumbleCoroutine);

        //놓았을때 이동량 기준으로 스핀 시작
        float releaseSpeed = rb.linearVelocity.magnitude;
        float initialSpin = releaseSpinMultiplier * releaseSpeed;
        tumbleCoroutine = StartCoroutine(TumbleCoroutine(initialSpin));

        currentHangTime = 0f;
        rope.startWidth = 0.1f;
        rb.linearVelocity = new Vector3(rb.linearVelocity.x * 0.1f,
        rb.linearVelocity.y*3f,
        rb.linearVelocity.z)* 0.1f;
        isSwing = false;
        rope.positionCount = 0;   // 라인 렌더러의 점 개수를 0으로 설정하여 선을 지움
        Destroy(cj);
        // if (sj) Destroy(sj);
        // sj = null;

        // if (rope)
        // {
        //     rope.startWidth = 0.1f;
        //     rope.positionCount = 0;
        //     // rope.enabled = false;
        // }

    }

    // 로프 시각화
    void DrawRope()
    {
        if (isSwing)
        {
            rope.SetPosition(0, transform.position);  // 로프의 첫 번째 점을 플레이어 위치로 설정하여 선을 그림
        }
    }
    // void MouseDown()
    // {
    //     if (isDragging) return; // 이미 드래그 중이면 무시 (한 번만 가능)
    //     isDragging = true;
    //     dragStartPos = Input.mousePosition;
    // }
    // void MouseDrag()
    // {
    //     Vector3 currentPos = Input.mousePosition;
    //     Vector3 dragDir = (currentPos - dragStartPos).normalized; // 방향
    //     if (dragDir.x < -0.1)
    //     {
    //         Debug.Log("좌");
    //         directionCheck += dragDir.x;
    //         rb.AddForce(Vector3.left * dragSpeed, ForceMode.Impulse);
    //     }
    //     if (dragDir.x > 0.1)
    //     {
    //         Debug.Log("우");
    //         directionCheck += dragDir.x;
    //         rb.AddForce(Vector3.right * dragSpeed, ForceMode.Impulse);
    //     }
    //     if (dragDir.y < 0)
    //     {
    //         Debug.Log("앞");
    //         rb.AddForce(Vector3.forward * dragSpeed, ForceMode.Impulse);
    //     }
    //     if (dragDir.y > 0)
    //     {
    //         Debug.Log("뒤");
    //         rb.AddForce(Vector3.back * dragSpeed / 3, ForceMode.Impulse);
    //     }
    //     Debug.Log(directionCheck);
    //     if (directionCheck < -0.5f)
    //     {
    //         cameraController.isLeft = true;
    //         cameraController.isRight = false;
    //     }
    //     else if (directionCheck > 0.5f)
    //     {
    //         cameraController.isRight = true;
    //         cameraController.isLeft = false;
    //     }
    //     dragStartPos = currentPos;
    // }
    void JumpStart()
    {
        rb.AddForce(transform.forward * 5f, ForceMode.Impulse);
        // rb.AddForce(Vector3.up * 5f, ForceMode.Impulse);
    }
    // Scene 뷰에서 범위를 표시
    void OnDrawGizmos()
    {
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        // 구체 시작 지점
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(ray.origin, radius);

        // 맞은 경우 충돌 지점
        if (hit.collider != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(hit.point, radius);
        }
        else
        {
            // 맞지 않았을 경우 끝 지점
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(ray.GetPoint(maxRayDistance), radius);
        }
    }

    // ─────────────────────────────────────────────
    // 착지 처리 (복구 코루틴 시작 트리거)
    void HandleLanding(GameObject collidedObject)
    {
        if (collidedObject.layer != LayerMask.NameToLayer("Bottom")) return;
        if (isSettling) return;

        // 스핀 중 착지
        if (tumbleCoroutine != null)
        {
            StopCoroutine(tumbleCoroutine);
            tumbleCoroutine = null;
            StartCoroutine(SettleRotationCoroutine()); // 아래에서 카메라 Yaw 사용하도록 수정됨
            return;
        }

        // 스핀 중이 아니어도, 착지 시 무조건 카메라 방향으로 맞추기
        if (alwaysAlignOnLanding)
            StartCoroutine(AlignToCameraYawOnLanding());
    }
    //바닥 충돌만 감지
    void OnCollisionEnter(Collision collision) => HandleLanding(collision.gameObject);
    void OnCollisionStay(Collision collision) => HandleLanding(collision.gameObject);

    // 공중 회전 코루틴
    private IEnumerator TumbleCoroutine(float initialSpinForce)
    {
        isSettling = false;
        if (playerController != null) playerController.IsTumbling = true;
        //x축만 회전 (앞으로만 돌게)
        // rb.constraints = RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
        // 받은 회전력만큼 회전
        rb.AddRelativeTorque(Vector3.right * initialSpinForce, ForceMode.Impulse);
        Debug.Log($"[Grapple] Tumble start, spinForce={initialSpinForce:F2}");
        //점수 측정용 회전 추적 시작
        rotationTracker?.StartTracking();

        while (true) yield return null;
    }

    // 착지 후 자세 복구(카메라 Yaw에 정렬)
    private IEnumerator SettleRotationCoroutine()
    {
        isSettling = true;
        Debug.Log("[Grapple] Landing: settle start");

        rotationTracker?.StopTracking();
        // 착지 직전까지 회전이 거의 1바퀴 이상 돌았으면 수동 스핀 카운트
        if (rotationTracker != null && rotationTracker.PendingAbsDegrees >= almostFullTurnThreshold)
        {
            RotationTracker.RaiseManualSpin();
            Debug.Log($"[Grapple] Almost full turn: +1 (pending={rotationTracker.PendingAbsDegrees:F1})");
        }

        // if (playerController != null) playerController.LockController();

        rb.isKinematic = true;

        // 카메라의 정면 방향을 계산하여 반환
        float targetYaw = CalcCameraYaw();
        Quaternion targetRotation = Quaternion.Euler(0f, targetYaw, 0f);
        //각도 차이가 1도 이하가 될 때까지 회전
        while (Quaternion.Angle(transform.rotation, targetRotation) > 1f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * settleSpeed);
            yield return null;
        }

        transform.rotation = targetRotation;
        // rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.isKinematic = false;

        if (playerController != null)
        {
            playerController.IsTumbling = false;
            // playerController.UnlockRotation();
            // playerController.UnlockController();
        }

        Debug.Log("[Grapple] Landing: settle end");
        isSettling = false;
    }

    float GetFlatYaw(Quaternion rot)
    {
        Vector3 fwd = rot * Vector3.forward;
        fwd.y = 0f;
        //입력 받은 방향이 너무 작으면 현재 플레이어 방향 유지
        if (fwd.sqrMagnitude < 1e-6f) return transform.eulerAngles.y;
        //Yaw(현재 바라보는 방향) 반환
        return Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
    }

    // 스핀 중엔 freeze된 CameraRig(고정된 시선)가 더 안정적, 그 외엔 실제 카메라 시선
    // 기존: 스핀 중엔 cameraRig(고정된 회전) 우선
    float CalcCameraYaw()
    {
        // ✅ 항상 "현재 카메라"의 시점을 기준으로 Yaw 계산
        if (cameraController != null) return GetFlatYaw(cameraController.transform.rotation);

        // 백업: 혹시 카메라 컨트롤러가 없을 때만 리그 사용
        if (cameraRig != null) return GetFlatYaw(cameraRig.transform.rotation);

        return transform.eulerAngles.y;
    }

    void SnapPlayerYawToCameraRig()
    {
        if (cameraRig == null) return;
        float yaw = GetFlatYaw(cameraRig.transform.rotation);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }
    //플레이어 방향을 카메라 시선으로 스냅
    void SnapPlayerYawToCameraView()
    {
        if (cameraController == null) return;
        float yaw = GetFlatYaw(cameraController.transform.rotation);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }
    //착지 시 카메라 방향으로 맞추기
    IEnumerator AlignToCameraYawOnLanding()
    {
        isSettling = true;
        // if (playerController != null) playerController.LockController();
        rb.isKinematic = true;
        // 카메라의 정면 방향을 계산하여 반환
        float targetYaw = CalcCameraYaw();
        Quaternion targetRot = Quaternion.Euler(0f, targetYaw, 0f);
        //카메라 방향으로 회전
        while (Quaternion.Angle(transform.rotation, targetRot) > 1f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * settleSpeed);
            yield return null;
        }

        transform.rotation = targetRot;
        // rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.isKinematic = false;

        if (playerController != null)
        {
            playerController.IsTumbling = false;
            // playerController.UnlockRotation();
            // playerController.UnlockController();
        }
        isSettling = false;
    }
    
    
}
