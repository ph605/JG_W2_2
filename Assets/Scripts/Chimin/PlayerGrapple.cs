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

    [Header("Aim/Raycast")]
    [SerializeField] LayerMask layerMask = ~0;
    [SerializeField] float rayDistance = 60f;
    [SerializeField] float sphereRadius = 3f;

    [Header("Spring Joint (Swing)")]
    [SerializeField] float springForce = 35f;
    [SerializeField] float springDamper = 4f;
    [SerializeField] float springMass = 1f;
    [SerializeField, Range(0.05f, 0.95f)] float minDistFrac = 0.2f;
    [SerializeField, Range(0.05f, 0.95f)] float maxDistFrac = 0.8f;
    [SerializeField] float maxSwingSpeed = 40f;

    [Header("Extra Push (optional)")]
    [SerializeField] bool enableWPush = true;
    [SerializeField] float pushForwardTop = 10f;
    [SerializeField] float pushForwardSide = 15f;
    [SerializeField] float pushUpSide = 5f;

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

    // 상태
    Rigidbody rb;
    SpringJoint sj;
    bool isSwing = false;
    Vector3 anchor;
    RaycastHit lastHit;
    int currentLayer = -1;

    // 입력
    InputAction grappleAction;
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

        if (!rotationTracker)
        {
            rotationTracker = GetComponent<RotationTracker>();
            if (!rotationTracker) rotationTracker = GetComponentInChildren<RotationTracker>(true);
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
        UpdateHookPoint();
        DrawRope();
        lastMousePos = Input.mousePosition;
    }

    void FixedUpdate()
    {
        if (!isSwing) return;

        if (rb.linearVelocity.sqrMagnitude > maxSwingSpeed * maxSwingSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSwingSpeed;

        if (enableWPush)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.wKey.isPressed)
            {
                if (currentLayer == LayerMask.NameToLayer("Left"))
                    rb.AddForce(transform.right * pushSideways, ForceMode.Force);
                else if (currentLayer == LayerMask.NameToLayer("Right"))
                    rb.AddForce(-transform.right * pushSideways, ForceMode.Force);
            }
        }

        if (enableWallPush)
        {
            if (currentLayer == LayerMask.NameToLayer("Left"))
                rb.AddForce(Vector3.right * pushSideways, ForceMode.Force);
            else if (currentLayer == LayerMask.NameToLayer("Right"))
                rb.AddForce(Vector3.left * pushSideways, ForceMode.Force);
        }

        if (rb.linearVelocity.sqrMagnitude > 0.1f)
        {
            Vector3 ropeDirection = (anchor - transform.position).normalized;
            Vector3 playerForward = rb.linearVelocity.normalized;
            Quaternion targetRotation = Quaternion.LookRotation(playerForward, ropeDirection);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * tiltSpeed));
        }
    }

    // 입력
    void OnGrappleStarted(InputAction.CallbackContext ctx)
    {
        TryStartSwing();
        lastMousePos = Input.mousePosition;
    }

    void OnGrappleCanceled(InputAction.CallbackContext ctx)
    {
        EndSwing();
    }

    // 에이밍
    void UpdateHookPoint()
    {
        if (!cam) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        bool got = Physics.Raycast(ray, out RaycastHit hit, rayDistance, layerMask, QueryTriggerInteraction.Collide);
        if (!got)
        {
            got = Physics.SphereCast(
              cam.transform.position,
              sphereRadius,
              cam.transform.forward,
              out hit,
              rayDistance,
              layerMask,
              QueryTriggerInteraction.Collide
            );
        }

        if (got)
        {
            lastHit = hit;
            if (aim && !aim.activeSelf) aim.SetActive(true);
            if (aim) aim.transform.position = hit.point;
        }
        else
        {
            lastHit = new RaycastHit();
            if (aim && aim.activeSelf) aim.SetActive(false);
        }
    }

    // 스윙 시작
    void TryStartSwing()
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

        if (isSwing) return;
        if (lastHit.point == Vector3.zero) return;

        isSwing = true;
        anchor = lastHit.point;
        currentLayer = lastHit.collider ? lastHit.collider.gameObject.layer : -1;

        if (playerController != null) playerController.LockRotation();
        if (cameraController != null) cameraController.EnterSwingView();

        if (rope)
        {
            rope.enabled = true;
            rope.positionCount = 2;
            rope.SetPosition(0, transform.position);
            rope.SetPosition(1, anchor);
        }

        if (!sj) sj = gameObject.AddComponent<SpringJoint>();
        sj.autoConfigureConnectedAnchor = false;
        sj.connectedAnchor = anchor;
        sj.spring = springForce;
        sj.damper = springDamper;
        sj.massScale = springMass;

        float dis = Vector3.Distance(transform.position, anchor);
        sj.maxDistance = Mathf.Max(0.01f, dis * maxDistFrac);
        sj.minDistance = Mathf.Clamp(dis * minDistFrac, 0f, sj.maxDistance);
    }

    // 스윙 종료
    void EndSwing()
    {
        if (!isSwing) return;

        isSwing = false;

        if (cameraController != null) cameraController.ExitSwingView();

        if (tumbleCoroutine != null) StopCoroutine(tumbleCoroutine);

        float releaseSpeed = rb.linearVelocity.magnitude;
        float initialSpin = releaseSpinMultiplier * releaseSpeed;
        tumbleCoroutine = StartCoroutine(TumbleCoroutine(initialSpin));

        rb.linearVelocity *= 0.5f;

        if (sj) Destroy(sj);
        sj = null;

        if (rope)
        {
            rope.positionCount = 0;
            rope.enabled = false;
        }
    }

    // 로프 시각화
    void DrawRope()
    {
        if (!isSwing || !rope) return;
        rope.SetPosition(0, transform.position);
        rope.SetPosition(1, anchor);
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

        while (true)
            yield return null;
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
        if (fwd.sqrMagnitude < 1e-6f)
            return transform.eulerAngles.y;

        return Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
    }

    // 스핀 중엔 freeze된 CameraRig(고정된 시선)가 더 안정적, 그 외엔 실제 카메라 시선
    // 기존: 스핀 중엔 cameraRig(고정된 회전) 우선
    float CalcCameraYaw()
    {
        // ✅ 항상 "현재 카메라"의 시점을 기준으로 Yaw 계산
        if (cameraController != null)
            return GetFlatYaw(cameraController.transform.rotation);

        // 백업: 혹시 카메라 컨트롤러가 없을 때만 리그 사용
        if (cameraRig != null)
            return GetFlatYaw(cameraRig.transform.rotation);

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

