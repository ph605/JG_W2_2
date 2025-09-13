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
    [SerializeField] CameraRigFollow cameraRig;

    [Header("Aim/Raycast")]
    [SerializeField] LayerMask layerMask = ~0;
    [SerializeField] float rayDistance = 60f;
    [SerializeField] float sphereRadius = 3f;

    // --- (수정) SpringJoint 관련 설정이 ConfigurableJoint 설정으로 변경되었습니다 ---
    [Header("Rope (Configurable Joint)")]
    [SerializeField] float minRopeDistance = 5f;
    [SerializeField] float hangingTime = 10f; // 매달리기 최대 시간
    [SerializeField] float swingGravityBoost = 5f; // 스윙 시 추가할 중력

    // --- (추가) 드래그 컨트롤 설정이 추가되었습니다 ---
    [Header("Drag Controls")]
    [SerializeField] float dragSpeed = 80f;
    [SerializeField] float dragTime = 1f;

    // --- 기존 기능 설정들은 모두 보존됩니다 ---
    [Header("Extra Push (optional)")]
    [SerializeField] bool enableWPush = true;
    [SerializeField] float pushSideways = 20f; // Wall Swing과 W Push가 사용하는 힘

    [Header("Release Physics & Landing")]
    [SerializeField] float releaseSpinMultiplier = 0.5f;
    [SerializeField] float settleSpeed = 8f;
    [SerializeField] bool alwaysAlignOnLanding = true;

    [Header("Swing Tilting")]
    [SerializeField] float tiltSpeed = 5f;
    [SerializeField] float maxSwingSpeed = 40f;

    [Header("Spin Correction")]
    [SerializeField] float almostFullTurnThreshold = 300f;


    // --- 상태 변수 ---
    Rigidbody rb;
    // SpringJoint sj; // (수정) SpringJoint 대신 ConfigurableJoint 사용
    ConfigurableJoint cj;
    bool isSwing = false;
    Vector3 anchor;
    RaycastHit lastHit;
    int currentLayer = -1;

    InputAction grappleAction;
    Vector3 lastMousePos;

    Coroutine tumbleCoroutine;
    private bool isSettling = false;

    // --- (추가) 드래그 및 타이머 상태 변수 ---
    private float currentHangTime = 0f;
    private float dragTimer = 0f;
    private bool isDragging = false;


    void Awake()
    {
        // Awake 로직은 기존과 동일합니다.
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
        // OnDestroy 로직은 기존과 동일합니다.
        if (grappleAction != null)
        {
            grappleAction.started -= OnGrappleStarted;
            grappleAction.canceled -= OnGrappleCanceled;
        }
    }

    void Update()
    {
        if (!isSwing)
        {
            UpdateHookPoint();
        }
        else
        {
            // --- (추가) 스윙 중일 때 타이머 로직 처리 ---
            currentHangTime += Time.deltaTime;
            if (currentHangTime >= hangingTime)
            {
                EndSwing(); // 시간이 다 되면 스윙 강제 종료
                return;
            }

            if (isDragging)
            {
                dragTimer += Time.deltaTime;
                if (dragTimer >= dragTime)
                {
                    isDragging = false; // 드래그 가능 시간 종료
                }
            }
        }

        DrawRope();
        lastMousePos = Input.mousePosition;
    }

    void FixedUpdate()
    {
        if (!isSwing) return;

        // 기존의 모든 FixedUpdate 로직을 보존합니다.
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

        // (기존 Wall Push는 W Push와 합쳐져 있으므로 별도 로직 불필요)

        if (rb.linearVelocity.sqrMagnitude > 0.1f)
        {
            Vector3 ropeDirection = (anchor - transform.position).normalized;
            Vector3 playerForward = rb.linearVelocity.normalized;
            Quaternion targetRotation = Quaternion.LookRotation(playerForward, ropeDirection);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * tiltSpeed));
        }

        // --- (추가) 새로운 물리 효과 및 드래그 힘 적용 ---
        rb.AddForce(Vector3.down * swingGravityBoost, ForceMode.Acceleration); // 추가 중력

        if (isDragging)
        {
            MouseDrag(); // 드래그 힘 적용
        }
    }

    void OnGrappleStarted(InputAction.CallbackContext ctx)
    {
        TryStartSwing();
        // (수정) 드래그 상태 초기화 추가
        isDragging = true;
        dragTimer = 0f;
    }

    void OnGrappleCanceled(InputAction.CallbackContext ctx)
    {
        EndSwing();
    }

    void UpdateHookPoint()
    {
        // 기존 Aiming 로직은 그대로 사용합니다.
        if (!cam) return;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        bool got = Physics.Raycast(ray, out RaycastHit hit, rayDistance, layerMask, QueryTriggerInteraction.Collide);
        if (!got)
        {
            got = Physics.SphereCast(cam.transform.position, sphereRadius, cam.transform.forward, out hit, rayDistance, layerMask, QueryTriggerInteraction.Collide);
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

    void TryStartSwing()
    {
        // 기존의 재스윙 시 자세 보정 로직은 모두 보존합니다.
        if (tumbleCoroutine != null)
        {
            StopCoroutine(tumbleCoroutine);
            isSettling = false;
            rb.isKinematic = false;
            rotationTracker?.StopTracking();
            SnapPlayerYawToCameraView();
            rb.angularVelocity = Vector3.zero;
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
        currentHangTime = 0f; // 타이머 초기화

        if (playerController != null) playerController.LockRotation();
        if (cameraController != null) cameraController.EnterSwingView();

        if (rope)
        {
            rope.enabled = true;
            rope.positionCount = 2;
            rope.SetPosition(0, transform.position);
            rope.SetPosition(1, anchor);
        }

        // --- (수정) SpringJoint를 ConfigurableJoint로 교체하는 로직 ---
        cj = gameObject.AddComponent<ConfigurableJoint>();
        cj.autoConfigureConnectedAnchor = false;
        cj.connectedAnchor = anchor;
        cj.xMotion = ConfigurableJointMotion.Limited;
        cj.yMotion = ConfigurableJointMotion.Limited;
        cj.zMotion = ConfigurableJointMotion.Limited;
        cj.angularXMotion = ConfigurableJointMotion.Free;
        cj.angularYMotion = ConfigurableJointMotion.Free;
        cj.angularZMotion = ConfigurableJointMotion.Free;

        SoftJointLimit limit = new SoftJointLimit();
        float distance = Vector3.Distance(transform.position, anchor);
        limit.limit = Mathf.Max(distance, minRopeDistance);
        cj.linearLimit = limit;
        // --- 여기까지 수정 ---
    }

    void EndSwing()
    {
        if (!isSwing) return;
        isSwing = false;
        isDragging = false; // 드래그 상태 해제

        if (cameraController != null) cameraController.ExitSwingView();

        // 기존의 Tumble(공중 회전) 시작 로직은 모두 보존합니다.
        if (tumbleCoroutine != null) StopCoroutine(tumbleCoroutine);
        float releaseSpeed = rb.linearVelocity.magnitude;
        float initialSpin = releaseSpinMultiplier * releaseSpeed;
        tumbleCoroutine = StartCoroutine(TumbleCoroutine(initialSpin));

        // 기존의 속도 감쇠 로직도 보존합니다.
        rb.linearVelocity *= 0.5f;

        // --- (수정) SpringJoint 대신 ConfigurableJoint 제거 ---
        if (cj) Destroy(cj);
        cj = null;

        if (rope)
        {
            rope.positionCount = 0;
            rope.enabled = false;
        }
    }

    // --- (추가) 마우스 드래그 기능 함수 ---
    void MouseDrag()
    {
        Vector3 currentMousePos = Input.mousePosition;
        Vector3 dragDelta = currentMousePos - lastMousePos;
        if (dragDelta.magnitude < 1f) return;

        Vector3 dragDir = dragDelta.normalized;

        if (dragDir.x < -0.2f) rb.AddForce(-cam.transform.right * dragSpeed, ForceMode.Impulse);
        if (dragDir.x > 0.2f) rb.AddForce(cam.transform.right * dragSpeed, ForceMode.Impulse);
        if (dragDir.y > 0.2f)
        {
            Vector3 pullDirection = (anchor - transform.position).normalized;
            rb.AddForce(pullDirection * dragSpeed, ForceMode.Impulse);
        }
        if (dragDir.y < -0.2f)
        {
            Vector3 pushDirection = (transform.position - anchor).normalized;
            rb.AddForce(pushDirection * dragSpeed, ForceMode.Impulse);
        }
    }

    void DrawRope()
    {
        if (!isSwing || !rope) return;
        rope.SetPosition(0, transform.position);
        rope.SetPosition(1, anchor);
    }

    // -------------------------------------------------------------------
    // 아래의 모든 착지 및 회전 관련 코드는 기존과 동일하게 완벽히 보존됩니다.
    // -------------------------------------------------------------------

    void HandleLanding(GameObject collidedObject)
    {
        if (collidedObject.layer != LayerMask.NameToLayer("Bottom")) return;
        if (isSettling) return;

        if (tumbleCoroutine != null)
        {
            StopCoroutine(tumbleCoroutine);
            tumbleCoroutine = null;
            StartCoroutine(SettleRotationCoroutine());
            return;
        }

        if (alwaysAlignOnLanding)
            StartCoroutine(AlignToCameraYawOnLanding());
    }

    void OnCollisionEnter(Collision collision) => HandleLanding(collision.gameObject);
    void OnCollisionStay(Collision collision) => HandleLanding(collision.gameObject);

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

    float CalcCameraYaw()
    {
        if (cameraController != null)
            return GetFlatYaw(cameraController.transform.rotation);
        if (cameraRig != null)
            return GetFlatYaw(cameraRig.transform.rotation);
        return transform.eulerAngles.y;
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