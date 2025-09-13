using UnityEngine;
using System;

public class RotationTracker : MonoBehaviour
{
    public static event Action OnRotationComplete;

    [Header("추적 대상(비우면 자기 자신)")]
    [SerializeField] Transform target;

    [Header("설정")]
    [SerializeField] float degreesPerRevolution = 360f; // 한 바퀴 기준
    [Tooltip("회전 방향 기준 축(로컬축). 보통 X-플립을 세면 Vector3.right")]
    [SerializeField] Vector3 localAxis = Vector3.right;

    [Header("Debug")]
    [SerializeField] bool debugLog = true;
    [SerializeField] float logEveryDegrees = 45f;

    bool isTracking = false;
    float totalRotation = 0f; // 누적 회전량(±deg)
    int lastLogStep = 0;
    Quaternion lastRot; // 이전 프레임의 월드 회전

    Transform T => target != null ? target : transform;

    public float RemainingAbsDegrees => Mathf.Abs(totalRotation); // 1) 미완 회전량(절대값) 공개
    public float PendingAbsDegrees => Mathf.Abs(totalRotation);

    public static void RaiseManualSpin()
    {
        OnRotationComplete?.Invoke();
    }

    public void StartTracking()
    {
        totalRotation = 0f;
        lastLogStep = 0;
        lastRot = T.rotation; // 월드 회전 저장
        isTracking = true;

        if (debugLog) Debug.Log("[Tracker] Start");
    }

    public void StopTracking()
    {
        isTracking = false;

        if (debugLog) Debug.Log("[Tracker] Stop");
    }

    void FixedUpdate()
    {
        if (!isTracking) return;

        // 현재/이전 회전으로 ΔQ 계산
        Quaternion curRot = T.rotation;
        Quaternion deltaQ = curRot * Quaternion.Inverse(lastRot);
        deltaQ.ToAngleAxis(out float angleDeg, out Vector3 axisWorld);

        // AngleAxis는 angle ∈ [0, 180]. 축 방향 부호로 signed 처리
        float sign = Mathf.Sign(Vector3.Dot(axisWorld, T.TransformDirection(localAxis)));
        float signedDelta = angleDeg * sign;
        totalRotation += signedDelta;

        // 디버그(지정 각도마다 1회)
        if (debugLog && logEveryDegrees > 0f)
        {
            int step = Mathf.FloorToInt(Mathf.Abs(totalRotation) / logEveryDegrees);
            if (step != lastLogStep)
            {
                lastLogStep = step;
                Debug.Log($"[Tracker] delta={signedDelta:F2}, total={totalRotation:F2}");
            }
        }

        // 여러 바퀴를 한 프레임에 넘길 수도 있으니 while
        while (Mathf.Abs(totalRotation) >= degreesPerRevolution)
        {
            if (debugLog) Debug.Log("[Tracker] One spin completed");
            OnRotationComplete?.Invoke();
            totalRotation -= degreesPerRevolution * Mathf.Sign(totalRotation);
        }

        lastRot = curRot;
    }
}
