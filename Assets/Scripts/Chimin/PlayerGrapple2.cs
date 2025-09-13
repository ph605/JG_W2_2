using JetBrains.Rider.Unity.Editor;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Burst.Intrinsics;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerGrapple2 : MonoBehaviour
{
    [SerializeField] PlayerInput playerInput;
    [SerializeField] RotationTracker rotationTracker;
    [SerializeField] CameraRigFollow cameraRig; // ← CameraRig 드롭
    public LayerMask layerMask;
    LineRenderer rope;
    ConfigurableJoint cj;
    Rigidbody rb;
    public GameObject aim;
    bool isSwing;
    [Header("Raycast")]
    public Camera cam;
    public float maxDistance;
    public float maxRopeDistance;
    public float minDistance;
    Vector3 spot;
    [Header("SphereCast Settings")]
    public float radius;      // 구체 반지름
    RaycastHit hit;
    [Header("Hang")]
    public float hangingTime;
    private float currentHangTime = 0;

    [Header("Drag")]
    private Vector3 dragStartPos;
    private bool isDragging;
    public float dragTime;
    public float dragSpeed;
    private float TempTime = 0;
    [Header("Gravity")]
    private bool isShowTime = false;
    public float showTime;
    private float showTempTime = 0;
    [Header("Swing Tilting")]
    [SerializeField] float tiltSpeed = 5f;
    [Header("Release Physics")]
    [SerializeField] float releaseSpinMultiplier = 0.5f;
    [SerializeField] float settleSpeed = 8f;
    [Header("Landing")]
    [SerializeField] bool alwaysAlignOnLanding = true; // ← 착지 시 항상 카메라 방향으로 맞추기
    [Header("Spin Correction")]
    [SerializeField] float almostFullTurnThreshold = 300f; // 270~330

    // --- 지민 ---
    [Header("Refs")]
    [SerializeField] PlayerController playerController;
    [SerializeField] PlayerCameraController cameraController;
    Vector3 lastMousePos;

    // 코루틴 상태
    private Coroutine tumbleCoroutine;
    private bool isSettling = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.maxAngularVelocity = Mathf.Infinity;

        if (!playerInput) playerInput = GetComponent<PlayerInput>();
        if (!playerController) playerController = GetComponent<PlayerController>();

        // if (playerInput != null)
        // {
        //     grappleAction = playerInput.actions["Grapple"];
        //     if (grappleAction != null)
        //     {
        //         grappleAction.started += OnGrappleStarted;
        //         grappleAction.canceled += OnGrappleCanceled;
        //     }
        // }

        if (rope)
        {
            rope.positionCount = 0;
            rope.useWorldSpace = true;
            rope.enabled = false;
        }

        if (!rotationTracker)
        {
            rotationTracker = GetComponent<RotationTracker>();
            if (!rotationTracker) rotationTracker = GetComponentInChildren<RotationTracker>(true);
        }
    }

    void Start()
    {
        rope = GetComponent<LineRenderer>();
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            JumpStart();
        }
        if (isShowTime)
        {
            showTempTime += Time.deltaTime;
            if (showTempTime >= showTime)
            {
                showTempTime = 0;
                isShowTime = false;
            }
        }
        else
        {
            rb.AddForce(Vector3.down * 8f, ForceMode.Acceleration);
        }
        if (isDragging)
        {
            TempTime += Time.deltaTime;
            if (TempTime >= dragTime)
            {
                TempTime = 0f;
                isDragging = false;
            }
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
                isDragging = false;
            }
        }
        else
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Physics.Raycast(ray, out hit, maxDistance, layerMask);
            aim.transform.position = ray.GetPoint(maxDistance);
            rb.maxLinearVelocity = 40f;
        }

        //마우스
        if (Input.GetMouseButtonDown(0))
        {
            if (isSwing)
            {

            }
            else
            {
                HookPoint();
                StartSwing();
                MouseDown();
                lastMousePos = Input.mousePosition; // 드래그 시작 위치
            }
        }
        else if (Input.GetMouseButton(0))
        {
            if (isSwing)
            {
                if (isDragging)
                {
                    MouseDrag();
                }
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            if (isSwing)
            {
                EndSwing();
                isDragging = false;
            }
        }
        DrawRope();
        lastMousePos = Input.mousePosition; // 매 프레임 갱신

        if (rb.linearVelocity.sqrMagnitude > 0.1f)
        {
            Vector3 ropeDirection = (spot - transform.position).normalized;
            Vector3 playerForward = rb.linearVelocity.normalized;
            Quaternion targetRotation = Quaternion.LookRotation(playerForward, ropeDirection);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * tiltSpeed));
        }
    }
    void HookPoint()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        // SphereCast로 충돌체 검사
        if (Physics.SphereCast(ray, radius, out hit, maxDistance, layerMask))
        {
            aim.transform.position = hit.point;
        }
    }
    void StartSwing()
    {
        if (hit.point == Vector3.zero) return;
        isSwing = true;
        isShowTime = true;
        spot = hit.point;   // 로프를 연결할 지점 설정
        try
        {
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
                    playerController.UnlockController();
                    playerController.UnlockRotation();
                }
            }

            rb.constraints = RigidbodyConstraints.FreezeRotation;
            playerController.LockRotation();
            cameraController.EnterSwingView();
        }
        catch
        {

        }
        rope.positionCount = 2;                   // 라인 렌더러의 점 개수 설정
        rope.SetPosition(0, transform.position);  // 첫 번째 점을 플레이어 위치로 설정
        rope.SetPosition(1, hit.point);           // 두 번째 점을 레이캐스트 위치로 설정

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
    void EndSwing()
    {
        try
        {
            playerController.UnlockRotation();
            cameraController.ExitSwingView();
            if (tumbleCoroutine != null) StopCoroutine(tumbleCoroutine);

            float releaseSpeed = rb.linearVelocity.magnitude;
            float initialSpin = releaseSpinMultiplier * releaseSpeed;
            tumbleCoroutine = StartCoroutine(TumbleCoroutine(initialSpin));
        }
        catch
        {

        }
        currentHangTime = 0f;
        rope.startWidth = 0.1f;
        rb.linearVelocity *= 0.5f;
        isSwing = false;
        rope.positionCount = 0;   // 라인 렌더러의 점 개수를 0으로 설정하여 선을 지움
        Destroy(cj);
    }
    void DrawRope()
    {
        if (isSwing)
        {
            rope.SetPosition(0, transform.position);  // 로프의 첫 번째 점을 플레이어 위치로 설정하여 선을 그림
        }
    }
    void MouseDown()
    {
        if (isDragging) return; // 이미 드래그 중이면 무시 (한 번만 가능)
        isDragging = true;
        dragStartPos = Input.mousePosition;
    }
    void MouseDrag()
    {
        Vector3 currentPos = Input.mousePosition;
        Vector3 dragDir = (currentPos - dragStartPos).normalized; // 방향
        if (dragDir.x < -0.2)
        {
            Debug.Log("좌");
            rb.AddForce(Vector3.left * dragSpeed, ForceMode.Impulse);
        }
        if (dragDir.x > 0.2)
        {
            rb.AddForce(Vector3.right * dragSpeed, ForceMode.Impulse);
        }
        if (dragDir.y < 0)
        {
            Debug.Log("앞");
            rb.AddForce(Vector3.forward * dragSpeed, ForceMode.Impulse);
        }
        if (dragDir.y > 0)
        {
            Debug.Log("뒤");
            rb.AddForce(Vector3.back * dragSpeed / 3, ForceMode.Impulse);
        }
        dragStartPos = currentPos;
    }
    void JumpStart()
    {
        rb.AddForce(Vector3.forward * 5f, ForceMode.Impulse);
        rb.AddForce(Vector3.up * 5f, ForceMode.Impulse);
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
            Gizmos.DrawWireSphere(ray.GetPoint(maxDistance), radius);
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

    void OnCollisionEnter(Collision collision) => HandleLanding(collision.gameObject);
    void OnCollisionStay(Collision collision) => HandleLanding(collision.gameObject);

    // 공중 회전 코루틴
    private IEnumerator TumbleCoroutine(float initialSpinForce)
    {
        isSettling = false;
        if (playerController != null) playerController.IsTumbling = true;

        rb.constraints = RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
        rb.AddRelativeTorque(Vector3.right * initialSpinForce, ForceMode.Impulse);
        Debug.Log($"[Grapple] Tumble start, spinForce={initialSpinForce:F2}");

        rotationTracker?.StartTracking();

        while (true) yield return null;
    }

    // 착지 후 자세 복구(카메라 Yaw에 정렬)
    private IEnumerator SettleRotationCoroutine()
    {
        isSettling = true;
        Debug.Log("[Grapple] Landing: settle start");

        rotationTracker?.StopTracking();

        if (rotationTracker != null && rotationTracker.PendingAbsDegrees >= almostFullTurnThreshold)
        {
            RotationTracker.RaiseManualSpin();
            Debug.Log($"[Grapple] Almost full turn: +1 (pending={rotationTracker.PendingAbsDegrees:F1})");
        }

        if (playerController != null) playerController.LockController();

        rb.isKinematic = true;

        // ★ 통일: 어떤 경우든 카메라가 보고 있는 수평 Yaw로 정렬
        float targetYaw = CalcCameraYaw();
        Quaternion targetRotation = Quaternion.Euler(0f, targetYaw, 0f);

        while (Quaternion.Angle(transform.rotation, targetRotation) > 1f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * settleSpeed);
            yield return null;
        }

        transform.rotation = targetRotation;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.isKinematic = false;

        if (playerController != null)
        {
            playerController.IsTumbling = false;
            playerController.UnlockRotation();
            playerController.UnlockController();
        }

        Debug.Log("[Grapple] Landing: settle end");
        isSettling = false;
    }

    float GetFlatYaw(Quaternion rot)
    {
        Vector3 fwd = rot * Vector3.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-6f) return transform.eulerAngles.y;
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

    void SnapPlayerYawToCameraView()
    {
        if (cameraController == null) return;
        float yaw = GetFlatYaw(cameraController.transform.rotation);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    IEnumerator AlignToCameraYawOnLanding()
    {
        isSettling = true;
        if (playerController != null) playerController.LockController();
        rb.isKinematic = true;

        float targetYaw = CalcCameraYaw();
        Quaternion targetRot = Quaternion.Euler(0f, targetYaw, 0f);

        while (Quaternion.Angle(transform.rotation, targetRot) > 1f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * settleSpeed);
            yield return null;
        }

        transform.rotation = targetRot;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.isKinematic = false;

        if (playerController != null)
        {
            playerController.IsTumbling = false;
            playerController.UnlockRotation();
            playerController.UnlockController();
        }

        isSettling = false;
    }
}