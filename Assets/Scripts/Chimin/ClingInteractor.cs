using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class ClingInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private GameObject aim;

    [Header("Holding Filter")]
    [SerializeField] private string holdingLayerName = "Holding";
    [SerializeField] private float rayDistance = 80f;

    [Header("Cling Settings")]
    [SerializeField] private float clingOffset = 0.12f;
    [SerializeField] private bool alignToSurface = true;
    [SerializeField] private bool parentWhileHolding = true;

    [Header("Velocity Handling")]
    [SerializeField] private bool zeroVelocityOnBegin = true;
    [SerializeField] private bool preserveHorizontalVelocityOnRelease = false;
    [SerializeField] private float releaseUpBoost = 0f;

    [Header("Cling Safety")]
    [SerializeField] private float pushOutSkin = 0.05f;
    [SerializeField] private int pushOutMaxIterations = 3;
    [SerializeField] private bool continuousPushOut = true;

    [Header("Release Safety")]
    [SerializeField] private float minOutwardSpeedOnRelease = 0.2f;
    [SerializeField] private CollisionDetectionMode releaseCCDMode = CollisionDetectionMode.ContinuousDynamic;

    [Header("Release Launch (Camera)")]
    [SerializeField] private bool launchTowardsCamera = true;
    [SerializeField] private float cameraLaunchSpeed = 20f;
    [SerializeField] private bool flattenCameraForward = true;

    [Header("Follow Camera Yaw After Release")]
    [Tooltip("홀딩 해제 후 비행/착지 직후 동안 카메라 회전에 맞춰 지속적으로 회전")]
    [SerializeField] private bool followCameraYawAfterRelease = true;
    [Tooltip("회전 보간 속도(값이 클수록 빨리 따라감)")]
    [SerializeField] private float yawFollowSlerpSpeed = 12f;
    [Tooltip("최소로 따라갈 시간(해제 직후)")]
    [SerializeField] private float yawFollowMinDuration = 0.35f;
    [Tooltip("첫 착지 후 추가로 따라갈 시간")]
    [SerializeField] private float yawFollowAfterLandingDuration = 0.35f;

    [Header("Begin Hold Soft-Snap")]
    [SerializeField] private bool softSnapOnBegin = true;
    [SerializeField, Min(0f)] private float beginMoveDuration = 0.5f;
    [SerializeField] private AnimationCurve beginMoveCurve = null; // 인스펙터에서 기본 EaseInOut 설정 권장
    [Tooltip("부드럽게 당기는 동안 라인 표시(플레이어↔히트지점)")]
    [SerializeField] private LineRenderer line;
    [SerializeField] private bool keepLineWhileHolding = false;
    [SerializeField] private float lineWidth = 0.03f;


    // ──────────────────────────────────────
    Rigidbody rb;
    Collider ownCol;

    bool isHolding = false;
    Transform holdTarget;
    Vector3 holdLocalPos;
    Quaternion holdLocalRot;
    Vector3 preHoldVelocity;
    int holdingLayer;

    Vector3 lastSurfaceNormal = Vector3.up;
    Collider[] holdTargetCols;

    // yaw-follow 상태
    Coroutine yawFollowRoutine;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ownCol = GetComponent<Collider>();

        if (cam == null) cam = Camera.main;
        if (playerController == null) playerController = GetComponent<PlayerController>();

        holdingLayer = LayerMask.NameToLayer(holdingLayerName);
        if (holdingLayer == -1)
            Debug.LogWarning($"[ClingInteractor] '{holdingLayerName}' 레이어 없음"); // 콘솔에서 바로 확인
        if (aim) aim.SetActive(false); // 시작 상태 통일


        // (옵션) 더 부드러운 회전/이동을 위해
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }


    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        UpdateHoldingAim();

        if (!isHolding)
        {
            if (mouse.leftButton.wasPressedThisFrame) TryBeginHold();
        }
        else
        {
            if (mouse.leftButton.isPressed) MaintainHold();
            if (mouse.leftButton.wasReleasedThisFrame) EndHold();
        }
    }

    // ── Aim for Holding layer
    void UpdateHoldingAim()
    {
        if (!cam || !aim) return;

        // 마우스가 없으면 화면 중앙 기준(패드 테스트용)
        Vector2 cursor = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        Ray ray = cam.ScreenPointToRay(cursor);

        // 레이어가 없으면 전체로 폴백
        int mask = (holdingLayer == -1) ? ~0 : (1 << holdingLayer);

        // 먼저 레이캐스트, 실패하면 스피어캐스트(맞추기 쉬움)
        bool got = Physics.Raycast(ray, out RaycastHit hit, rayDistance, mask, QueryTriggerInteraction.Collide);
        if (!got)
            got = Physics.SphereCast(ray, 0.2f, out hit, rayDistance, mask, QueryTriggerInteraction.Collide);

        if (got)
        {
            if (!aim.activeSelf) aim.SetActive(true);
            aim.transform.position = hit.point;
        }
        else
        {
            if (aim.activeSelf) aim.SetActive(false);
        }
    }


    void TryBeginHold()
    {
        if (cam == null || holdingLayer == -1) return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        int mask = 1 << holdingLayer;

        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, mask, QueryTriggerInteraction.Collide))
            BeginHold(hit);
    }

    void BeginHold(RaycastHit hit)
    {
        var sj = GetComponent<SpringJoint>();
        if (sj != null) Destroy(sj);

        if (yawFollowRoutine != null) { StopCoroutine(yawFollowRoutine); yawFollowRoutine = null; }

        preHoldVelocity = rb.linearVelocity;

        lastSurfaceNormal = hit.normal;
        Vector3 anchorWorldPos = hit.point + lastSurfaceNormal * clingOffset;
        Quaternion anchorWorldRot = alignToSurface
            ? Quaternion.LookRotation(-lastSurfaceNormal, Vector3.up)
            : transform.rotation;

        holdTarget = hit.collider.transform;
        holdTargetCols = holdTarget.GetComponentsInChildren<Collider>();

        // 목표 지점도 일단 안전 보정
        anchorWorldPos = GetSafeAnchor(holdTarget, anchorWorldPos, anchorWorldRot);

        if (zeroVelocityOnBegin)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 컨트롤 잠금 및 상태 플래그 (isHolding은 소프트 스냅 완료 후에 true)
        rb.isKinematic = true;
        if (playerController != null)
        {
            playerController.IsClinging = true;
            playerController.LockController();
            playerController.LockRotation();
        }

        if (softSnapOnBegin)
        {
            if (beginMoveCurve == null) beginMoveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            StartCoroutine(SoftSnapToAnchor(hit.point, anchorWorldPos, anchorWorldRot));
        }
        else
        {
            // 기존 즉시 스냅 로직(필요하면 유지)
            transform.SetPositionAndRotation(anchorWorldPos, anchorWorldRot);
            if (parentWhileHolding && holdTarget != null)
                transform.SetParent(holdTarget, true);

            holdLocalPos = holdTarget.InverseTransformPoint(anchorWorldPos);
            holdLocalRot = Quaternion.Inverse(holdTarget.rotation) * anchorWorldRot;

            isHolding = true;
        }
    }


    void MaintainHold()
    {
        if (holdTarget == null) return;

        if (!parentWhileHolding)
        {
            Vector3 worldPos = holdTarget.TransformPoint(holdLocalPos);
            Quaternion worldRot = holdTarget.rotation * holdLocalRot;
            transform.SetPositionAndRotation(worldPos, worldRot);
        }

        if (continuousPushOut)
        {
            Vector3 safe = GetSafeAnchor(holdTarget, transform.position, transform.rotation);
            if ((safe - transform.position).sqrMagnitude > 1e-10f)
                transform.position = safe;
        }

        // 라인 유지 옵션
        if (line && keepLineWhileHolding)
        {
            line.enabled = true;
            line.positionCount = 2;
            line.SetPosition(0, transform.position);
            line.SetPosition(1, holdTarget.TransformPoint(holdLocalPos)); // 현재 앵커 지점
        }
    }


    void EndHold()
    {
        if (!isHolding) return;

        // 부모 해제 전 살짝 밖으로
        Vector3 preReleasePos = transform.position + lastSurfaceNormal * pushOutSkin;
        Vector3 safePos = GetSafeAnchor(holdTarget, preReleasePos, transform.rotation);
        transform.position = safePos;
        Physics.SyncTransforms();

        if (parentWhileHolding) transform.SetParent(null, true);

        rb.isKinematic = false;
        rb.collisionDetectionMode = releaseCCDMode;

        // 기본 속도 구성
        Vector3 v = rb.linearVelocity;
        if (preserveHorizontalVelocityOnRelease)
        {
            Vector3 horiz = new Vector3(preHoldVelocity.x, 0f, preHoldVelocity.z);
            v.x = horiz.x; v.z = horiz.z;
        }
        if (releaseUpBoost > 0f) v.y = Mathf.Max(v.y, releaseUpBoost);

        // 카메라 방향 런치
        if (launchTowardsCamera && cam != null)
        {
            Vector3 dir = cam.transform.forward;
            if (flattenCameraForward) dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) dir = transform.forward;
            dir.Normalize();

            Vector3 launchVel = dir * cameraLaunchSpeed;
            if (flattenCameraForward) launchVel.y = v.y; // 수직은 유지
            v = launchVel;
        }

        // 표면 바깥 성분 최소 보장
        float outward = Vector3.Dot(v, lastSurfaceNormal);
        if (outward < minOutwardSpeedOnRelease)
            v += lastSurfaceNormal * (minOutwardSpeedOnRelease - outward);

        rb.linearVelocity = v;

        if (playerController != null)
        {
            playerController.IsClinging = false;
            playerController.UnlockController();
            playerController.UnlockRotation();   
        }

        // ✅ 해제 후 카메라 회전에 지속적으로 맞추는 코루틴 시작
        if (followCameraYawAfterRelease && yawFollowRoutine == null)
            yawFollowRoutine = StartCoroutine(FollowCameraYawRoutine());

        isHolding = false;
        holdTarget = null;
        holdTargetCols = null;
    }

    // 비행/착지 직후 카메라 회전에 맞춰 계속 Yaw를 정렬
    IEnumerator FollowCameraYawRoutine()
    {
        float timer = yawFollowMinDuration; // 설정한 최소 시간만큼만 유지

        while (timer > 0f)
        {
            // 카메라 방향 계산
            Vector3 dir = (cam != null) ? cam.transform.forward : transform.forward;
            if (flattenCameraForward) dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) dir = transform.forward;
            dir.Normalize();

            // 목표 회전으로 부드럽게 정렬 (플레이어 회전 잠금 X)
            Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
            Quaternion newRot = Quaternion.Slerp(
                rb.rotation,
                target,
                Mathf.Clamp01(Time.deltaTime * yawFollowSlerpSpeed)
            );
            rb.MoveRotation(newRot);

            timer -= Time.deltaTime;
            yield return null;
        }

        yawFollowRoutine = null;
    }



    // ─────────────────────────────────────────────
    Vector3 GetSafeAnchor(Transform targetRoot, Vector3 desiredPos, Quaternion desiredRot)
    {
        if (!ownCol || !targetRoot) return desiredPos;

        var targetCols = targetRoot.GetComponentsInChildren<Collider>();
        if (targetCols == null || targetCols.Length == 0) return desiredPos;

        Vector3 pos = desiredPos;

        for (int i = 0; i < pushOutMaxIterations; i++)
        {
            bool overlappedAny = false;

            foreach (var targetCol in targetCols)
            {
                if (!targetCol || !targetCol.enabled) continue;

                Vector3 dir; float dist;
                bool overlapped = Physics.ComputePenetration(
                    ownCol, pos, desiredRot,
                    targetCol, targetCol.transform.position, targetCol.transform.rotation,
                    out dir, out dist
                );

                if (overlapped)
                {
                    pos += dir * (dist + pushOutSkin);
                    overlappedAny = true;
                }
            }

            if (!overlappedAny) break;
        }

        return pos;
    }

    IEnumerator SoftSnapToAnchor(Vector3 hitPoint, Vector3 anchorPos, Quaternion anchorRot)
    {
        if (line)
        {
            line.enabled = true;
            line.positionCount = 2;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
        }

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        float t = 0f;
        while (t < beginMoveDuration)
        {
            float u = beginMoveCurve.Evaluate(t / beginMoveDuration);

            Vector3 rawPos = Vector3.Lerp(startPos, anchorPos, u);
            Quaternion rawRot = Quaternion.Slerp(startRot, anchorRot, u);

            Vector3 safePos = GetSafeAnchor(holdTarget, rawPos, rawRot);

            rb.MovePosition(safePos);
            rb.MoveRotation(rawRot);

            if (line)
            {
                // 물리 스텝 기준으로 바로 보이도록 rb.position 사용
                line.SetPosition(0, rb.position);
                line.SetPosition(1, hitPoint);
            }

            // 거의 붙었으면 조기 종료(끊김 방지)
            if ((anchorPos - rb.position).sqrMagnitude < 0.0004f &&
                Quaternion.Angle(rb.rotation, anchorRot) < 0.5f)
                break;

            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();   // ★ 핵심: 물리 스텝으로 동기화
        }

        Vector3 finalPos = GetSafeAnchor(holdTarget, anchorPos, anchorRot);
        rb.MovePosition(finalPos);
        rb.MoveRotation(anchorRot);
        Physics.SyncTransforms();

        if (parentWhileHolding && holdTarget != null)
            transform.SetParent(holdTarget, true);

        holdLocalPos = holdTarget.InverseTransformPoint(finalPos);
        holdLocalRot = Quaternion.Inverse(holdTarget.rotation) * anchorRot;

        isHolding = true;

        if (line && !keepLineWhileHolding)
            line.enabled = false;
    }

}
