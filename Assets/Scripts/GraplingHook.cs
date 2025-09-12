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
    ConfigurableJoint cj;
    Rigidbody rb;
    public GameObject aim;


    float dis;
    Vector3 spot;
    bool isSwing;

    [Header("Raycast")]
    public Camera cam;
    public float maxDistance;
    public float minDistance;

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
            // UpdateSpringForceByDrag();
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
            if (factor == 0)
            {
                factor = 0.001f;
            }
            Debug.Log(rb.maxLinearVelocity = 40f * factor);
            rb.maxLinearVelocity = 40f * factor;
        }
        else
        {
            HookPoint();
            rb.maxLinearVelocity = 40f;
        }
        if (Input.GetMouseButtonDown(0) && !isSwing)
        {
            StartSwing();
            lastMousePos = Input.mousePosition; // 드래그 시작 위치
        }
        else if (Input.GetMouseButtonUp(0) && isSwing)
        {
            EndSwing();
        }
        DrawRope();
        lastMousePos = Input.mousePosition; // 매 프레임 갱신
    }
    void UpdateSpringForceByDrag()
    {
        Vector3 mouseDelta = Input.mousePosition - lastMousePos;

        // 거의 움직이지 않으면 무시
        if (mouseDelta.sqrMagnitude < 0.01f) return;

        if (mouseDelta.y < 0) // 뒤로 드래그할 때만 힘 적용
        {
            rb.AddForce(Vector3.forward * 15.0f , ForceMode.Force);
        }
    }


    void HookPoint()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Physics.Raycast(ray, out hit, maxDistance, layerMask);
        aim.transform.position = hit.point;
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
        /*
        sj = gameObject.AddComponent<SpringJoint>();    // 스프링 조인트 컴포넌트 추가
        sj.autoConfigureConnectedAnchor = false;        // 연결된 앵커 자동 설정 비활성화
        sj.connectedAnchor = spot;                      // 연결 앵커를 훅 지점으로 설정

    sj.spring = springForce;    // 스프링 힘 설정
    sj.damper = springDamper;   // 스프링 댐퍼 설정
    sj.massScale = springMass;  // 스프링 질량 설정

    dis = Vector3.Distance(transform.position, spot);   // 플레이어와 로프 연결 지점 간의 거리 계산

        sj.maxDistance = dis * 0.8f;    // 스프링의 최대 길이 설정
        sj.minDistance = dis * 0.2f;    // 스프링의 최소 길이 설정
        */
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
        rb.linearVelocity = new Vector3(rb.linearVelocity.x * 0.5f,
        rb.linearVelocity.y * 0.8f,
        rb.linearVelocity.z);
        isSwing = false;
        lr.positionCount = 0;   // 라인 렌더러의 점 개수를 0으로 설정하여 선을 지움
        // Destroy(sj);            // 스프링 조인트 컴포넌트 파괴
        Destroy(cj);
    }

void DrawRope()
{
    if (isSwing)
    {
        lr.SetPosition(0, transform.position);  // 로프의 첫 번째 점을 플레이어 위치로 설정하여 선을 그림
    }
}


}