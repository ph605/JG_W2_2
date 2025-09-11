using JetBrains.Rider.Unity.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Burst.Intrinsics;
using UnityEngine;

public class GrapplingHook : MonoBehaviour
{
    public String currentTag = "Top";
    public LayerMask layerMask;
    RaycastHit hit;
    LineRenderer lr;
    SpringJoint sj;
    Rigidbody rb;
    public GameObject aim;


float dis;
    Vector3 spot;
    bool isSwing;

    [Header("Raycast")]
    public Camera cam;
    public float rayDistance;

    [Header("SpringJoint")]
    public float springForce;
    public float springDamper;
    public float springMass;

    // --- 지민 ---
    [Header("Dependencies")]
    public CameraController cameraController; // CameraController 참조 변수 추가
                                              // -------------------

    Vector3 lastMousePos;
    void Start()
    {
        lr = GetComponent<LineRenderer>();
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        HookPoint();

        if (Input.GetMouseButtonDown(0) && !isSwing)
        {
            StartSwing();
            lastMousePos = Input.mousePosition; // 드래그 시작 위치
        }
        else if (Input.GetMouseButtonUp(0) && isSwing)
        {
            EndSwing();
        }

        if (isSwing)
        {
            Vector3 hookDir = (spot - transform.position).normalized;
            Vector3 hookNormal = hit.normal;

            // Y축 기준으로 signed angle
            float angle = Vector3.SignedAngle(hookDir, hookNormal, Vector3.up); // -180 ~ 180


            // 각도가 90도에 가까울수록 힘 감소
            float absAngle = Mathf.Abs(angle); // 0~180
            float factor = Mathf.InverseLerp(90f, 180f, absAngle);
            Debug.Log(factor);
            if (Input.GetKey(KeyCode.W))
            {
                if (currentTag == "Top")
                {
                    rb.AddForce(Vector3.forward * 10.0f, ForceMode.Force);
                }
                if (currentTag == "Left")
                if (currentTag == "Left")
                {
                    rb.AddForce(Vector3.forward * 15.0f, ForceMode.Force);
                    rb.AddForce(Vector3.up * 5.0f, ForceMode.Force);
                }
                if (currentTag == "Right")
                if (currentTag == "Right")
                {
                    rb.AddForce(Vector3.forward * 15.0f, ForceMode.Force);
                    rb.AddForce(Vector3.up * 5.0f, ForceMode.Force);
                }
            }
            // Debug.Log(rb.maxLinearVelocity = 30f * factor);
            rb.maxLinearVelocity = 40f * factor;
        }
        else
        {
            rb.maxLinearVelocity = 40f;
        }
        DrawRope();
        lastMousePos = Input.mousePosition; // 매 프레임 갱신
    }
    void UpdateSpringForceByDrag()
    {
        Vector3 mouseDelta = Input.mousePosition - lastMousePos;

        // 거의 움직이지 않으면 무시
        if (mouseDelta.sqrMagnitude < 0.01f) return;

        // 캐릭터 → 훅 지점 방향 (월드 공간)
        Vector3 hookDir = (spot - transform.position).normalized;

        // 훅 지점 법선 벡터
        Vector3 hookNormal = hit.normal;

        // 두 벡터 사이 각도 (0 ~ 180)
        float angle = Vector3.Angle(hookDir, hookNormal);

        // 각도가 90도에 가까울수록 힘 감소
        float factor = Mathf.Cos(angle * Mathf.Deg2Rad);
        factor = Mathf.Clamp(factor, 0.2f, 1f); // 최소 0.2로 설정, 너무 느리면 스윙이 답답해짐

        if (mouseDelta.y < 0) // 뒤로 드래그할 때만 힘 적용
        {
            rb.linearDamping = 0;
            // 힘에 factor 곱해서 벽 근처에서 속도 감소
            rb.AddForce(Vector3.forward * 0.04f * factor, ForceMode.Force);

            if (sj != null)
            {
                // sj.spring = mappedValue * factor; // 필요 시 스프링도 조절 가능
                // sj.damper = mappedValue * factor;
            }
        }
        else
        {
            if (sj != null)
            {
                rb.linearDamping += 0.002f;
            }
        }
    }


    void HookPoint()
    {
        
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Physics.Raycast(ray, out hit, rayDistance,layerMask);
        Debug.DrawRay(spot, hit.normal * 2f, Color.red); // 법선 벡터를 빨간색으로 표시
        RaycastHit sphereCastHit;
        Physics.SphereCast(cam.transform.position, 4, cam.transform.forward, out sphereCastHit, rayDistance,layerMask);

    Vector3 realHitPoint;

        if (hit.point != Vector3.zero)
        {
            realHitPoint = hit.point;
        }
        else if (sphereCastHit.point != Vector3.zero)
        {
            realHitPoint = sphereCastHit.point;
        }
        aim.transform.position = hit.point;

        hit = hit.point == Vector3.zero ? sphereCastHit : hit;
    }

void StartSwing()
{
    if (hit.point == Vector3.zero) return;

    isSwing = true;

    spot = hit.point;   // 로프를 연결할 지점 설정
    if (hit.transform.CompareTag("Top"))
    {
        currentTag = "Top";
    }
    if (hit.transform.CompareTag("Left"))
    {
        currentTag = "Left";
    }
    if (hit.transform.CompareTag("Right"))
    {
        currentTag = "Right";
    }

    // --- 지민 ---
    // 스윙 시작 시 카메라 컨트롤러에 로프를 건 '위치(spot)'를 전달
    cameraController?.EnterSwingView(spot);
    // -------------------

    lr.positionCount = 2;                   // 라인 렌더러의 점 개수 설정
    lr.SetPosition(0, transform.position);  // 첫 번째 점을 플레이어 위치로 설정
    lr.SetPosition(1, hit.point);           // 두 번째 점을 레이캐스트 위치로 설정

    sj = gameObject.AddComponent<SpringJoint>();    // 스프링 조인트 컴포넌트 추가
    sj.autoConfigureConnectedAnchor = false;        // 연결된 앵커 자동 설정 비활성화
    sj.connectedAnchor = spot;                      // 연결 앵커를 훅 지점으로 설정

    sj.spring = springForce;    // 스프링 힘 설정
    sj.damper = springDamper;   // 스프링 댐퍼 설정
    sj.massScale = springMass;  // 스프링 질량 설정

    dis = Vector3.Distance(transform.position, spot);   // 플레이어와 로프 연결 지점 간의 거리 계산

    sj.maxDistance = dis * 0.8f;    // 스프링의 최대 길이 설정
    sj.minDistance = dis * 0.2f;    // 스프링의 최소 길이 설정
}

    void EndSwing()
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x * 0.5f,
        rb.linearVelocity.y * 0.8f,
        rb.linearVelocity.z);
        isSwing = false;
        lr.positionCount = 0;   // 라인 렌더러의 점 개수를 0으로 설정하여 선을 지움
        Destroy(sj);            // 스프링 조인트 컴포넌트 파괴
    }

void DrawRope()
{
    if (isSwing)
    {
        lr.SetPosition(0, transform.position);  // 로프의 첫 번째 점을 플레이어 위치로 설정하여 선을 그림
    }
}


}