using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class ClingInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private GameObject aim; // 선택

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

    [Header("Release Align (Camera)")]
    [Tooltip("홀딩 해제 시 플레이어 Yaw를 카메라 방향으로 맞춤")]
    [SerializeField] private bool alignYawToCameraOnRelease = true;
    [Tooltip("스냅 대신 부드럽게 회전할지")]
    [SerializeField] private bool smoothAlignYawOnRelease = true;
    [Tooltip("부드러운 정렬에 걸리는 시간(초)")]
    [SerializeField, Min(0f)] private float alignYawDuration = 0.25f;
    [Tooltip("정렬 동안 플레이어 수동 회전 잠금")]
    [SerializeField] private bool lockRotationDuringAlign = true;

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
    Collider[] holdTargetCols; // 자식 포함 캐시

    // 부드러운 정렬 코루틴 핸들
    Coroutine alignYawRoutine;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ownCol = GetComponent<Collider>();

        if (cam == null) cam = Camera.main;
        if (playerController == null) playerController = GetComponent<PlayerController>();

        holdingLayer = LayerMask.NameToLayer(holdingLayerName);
        if (holdingLayer == -1)
            Debug.LogWarning($"[ClingInteractor] '{holdingLayerName}' 레이어를 찾지 못했습니다.");
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        UpdateHoldingAim();

        if (!isHolding)
        {
            if (mouse.leftButton.wasPressedThisFrame)
                TryBeginHold();
        }
        else
        {
            if (mouse.leftButton.isPressed)
                MaintainHold();

            if (mouse.leftButton.wasReleasedThisFrame)
                EndHold();
        }
    }

    // Holding 레이어도 에임 표시
    void UpdateHoldingAim()
    {
        if (!cam || holdingLayer == -1 || !aim) return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        int mask = 1 << holdingLayer;

        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, mask, QueryTriggerInteraction.Collide))
        {
            if (!aim.activeSelf) aim.SetActive(true);
            aim.transform.position = hit.point;
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

        // 정렬 코루틴 돌고 있으면 중단
        if (alignYawRoutine != null) { StopCoroutine(alignYawRoutine); alignYawRoutine = null; }

        preHoldVelocity = rb.linearVelocity;

        lastSurfaceNormal = hit.normal;
        Vector3 anchorWorldPos = hit.point + lastSurfaceNormal * clingOffset;
        Quaternion anchorWorldRot = alignToSurface
            ? Quaternion.LookRotation(-lastSurfaceNormal, Vector3.up)
            : transform.rotation;

        holdTarget = hit.collider.transform;
        holdTargetCols = holdTarget.GetComponentsInChildren<Collider>();

        // 시작 시에도 무조건 콜라이더 밖으로
        anchorWorldPos = GetSafeAnchor(holdTarget, anchorWorldPos, anchorWorldRot);

        if (zeroVelocityOnBegin)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        rb.isKinematic = true;
        transform.SetPositionAndRotation(anchorWorldPos, anchorWorldRot);

        holdLocalPos = holdTarget.InverseTransformPoint(anchorWorldPos);
        holdLocalRot = Quaternion.Inverse(holdTarget.rotation) * anchorWorldRot;

        if (parentWhileHolding && holdTarget != null)
            transform.SetParent(holdTarget, true);

        if (playerController != null)
        {
            playerController.IsClinging = true;
            playerController.LockController();
            playerController.LockRotation();
        }

        isHolding = true;
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
    }

    void EndHold()
    {
        if (!isHolding) return;

        // 부모 해제 전 마지막 바깥쪽으로 스냅
        Vector3 preReleasePos = transform.position + lastSurfaceNormal * pushOutSkin;
        Vector3 safePos = GetSafeAnchor(holdTarget, preReleasePos, transform.rotation);
        transform.position = safePos;
        Physics.SyncTransforms();

        // 카메라 기준 방향 계산(속도/정렬에 공용 사용)
        Vector3 camDir = (cam != null) ? cam.transform.forward : transform.forward;
        if (flattenCameraForward) camDir.y = 0f;
        if (camDir.sqrMagnitude < 1e-6f) camDir = transform.forward;
        camDir.Normalize();

        // ✅ 스무스 Yaw 정렬
        if (alignYawToCameraOnRelease)
        {
            if (alignYawRoutine != null) StopCoroutine(alignYawRoutine);
            alignYawRoutine = smoothAlignYawOnRelease
                ? StartCoroutine(AlignYawSmoothlyToCamera(camDir, alignYawDuration))
                : StartCoroutine(AlignYawSmoothlyToCamera(camDir, 0f)); // 0이면 즉시 스냅
        }

        if (parentWhileHolding)
            transform.SetParent(null, true);

        rb.isKinematic = false;
        rb.collisionDetectionMode = releaseCCDMode;

        // 기본 속도 구성
        Vector3 v = rb.linearVelocity;

        if (preserveHorizontalVelocityOnRelease)
        {
            Vector3 horiz = new Vector3(preHoldVelocity.x, 0f, preHoldVelocity.z);
            v.x = horiz.x; v.z = horiz.z;
        }

        if (releaseUpBoost > 0f)
            v.y = Mathf.Max(v.y, releaseUpBoost);

        // (옵션) 카메라 방향으로 런치
        if (launchTowardsCamera)
        {
            Vector3 launchVel = camDir * cameraLaunchSpeed;
            if (flattenCameraForward) launchVel.y = v.y; // 수평만 교체
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
            // 회전은 정렬 코루틴 내에서 잠금 해제(옵션)하므로 여기선 조작만 해제
            playerController.UnlockController();
        }

        isHolding = false;
        holdTarget = null;
        holdTargetCols = null;
    }

    // 원하는 포즈에서 타겟과 겹치면 ComputePenetration으로 밖으로 밀어냄
    Vector3 GetSafeAnchor(Transform targetRoot, Vector3 desiredPos, Quaternion desiredRot)
    {
        if (!ownCol || !targetRoot) return desiredPos;

        var targetCols = (targetRoot == holdTarget && holdTargetCols != null && holdTargetCols.Length > 0)
            ? holdTargetCols
            : targetRoot.GetComponentsInChildren<Collider>();

        if (targetCols == null || targetCols.Length == 0)
            return desiredPos;

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

    // ──────────────────────────────────────
    // 카메라 방향으로 플레이어 Yaw를 부드럽게 맞춘다.
    IEnumerator AlignYawSmoothlyToCamera(Vector3 targetDir, float duration)
    {
        if (targetDir.sqrMagnitude < 1e-6f) yield break;

        Quaternion start = transform.rotation;
        Quaternion target = Quaternion.LookRotation(targetDir.normalized, Vector3.up);

        // 회전 중 입력과 충돌 방지(옵션)
        if (lockRotationDuringAlign && playerController != null)
            playerController.LockRotation();

        // duration == 0 이면 즉시 스냅
        if (duration <= 0f)
        {
            if (!rb.isKinematic) rb.MoveRotation(target);
            else transform.rotation = target;

            if (lockRotationDuringAlign && playerController != null)
                playerController.UnlockRotation();

            alignYawRoutine = null;
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            Quaternion q = Quaternion.Slerp(start, target, s);

            if (!rb.isKinematic) rb.MoveRotation(q);
            else transform.rotation = q;

            yield return null;
        }

        if (!rb.isKinematic) rb.MoveRotation(target);
        else transform.rotation = target;

        if (lockRotationDuringAlign && playerController != null)
            playerController.UnlockRotation();

        alignYawRoutine = null;
    }
}
