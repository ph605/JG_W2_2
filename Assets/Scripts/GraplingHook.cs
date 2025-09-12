using JetBrains.Rider.Unity.Editor;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Burst.Intrinsics;
using UnityEngine;
using UnityEngine.InputSystem;

public class GrapplingHook : MonoBehaviour
{
    public LayerMask layerMask;
    
    LineRenderer lr;
    SpringJoint sj;
    ConfigurableJoint cj;
    Rigidbody rb;
    public GameObject aim;

    
    float dis;
   
    bool isSwing;

    [Header("Raycast")]
    public Camera cam;
    public float maxDistance;
    public float minDistance;
    Vector3 spot;
    RaycastHit hit;
    [Header("Hang")]
    public float hangingTime;
    private float currentHangTime = 0;

    [Header("Drag")]
    private Vector3 dragStartPos;
    private bool isDragging;
    public float dragTime;
    [SerializeField]
    private float TempTime;

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
            currentHangTime += Time.deltaTime;
            float t = currentHangTime / hangingTime;
            float currentWidth = Mathf.Lerp(0.1f, 0f, t);
            lr.startWidth = currentWidth;
            lr.endWidth = currentWidth;
            if (currentHangTime >= hangingTime)
            {
                
                EndSwing();
                currentHangTime = 0f;
                lr.startWidth = 0.1f;
                isDragging = false;
            }
            //  Vector3 dirToAnchor = (spot - transform.position).normalized;

            // // 캐릭터가 바라보는 정면 (forward)
            // Vector3 forward = transform.forward;
            // // Y축 기준으로 signed angle
            // float signedAngle = Vector3.SignedAngle(transform.position, dirToAnchor, Vector3.up);

            // float value = 1f - Mathf.Abs(signedAngle - 90f) / 90f;
            // float factor = Mathf.InverseLerp(0f, 180f, Mathf.Abs(signedAngle));
            // if (Input.GetKey(KeyCode.W))
            // {
            //     rb.AddForce(Vector3.forward * 5f, ForceMode.Acceleration);
            //     // rb.AddForce(hookNormal * 5.0f, ForceMode.Force);
            //     // rb.AddForce(Vector3.up * 10.0f, ForceMode.Force);
            //     // if (currentTag == "Top")
            //     // {
            //     //     rb.AddForce(Vector3.forward * 10.0f, ForceMode.Force);
            //     // }
            //     // if (currentTag == "Left")
            //     // if (currentTag == "Left")
            //     // {
            //     //     rb.AddForce(Vector3.forward * 15.0f, ForceMode.Force);
            //     //     rb.AddForce(Vector3.up * 5.0f, ForceMode.Force);
            //     // }
            //     // if (currentTag == "Right")
            //     // if (currentTag == "Right")
            //     // {
            //     //     rb.AddForce(Vector3.forward * 15.0f, ForceMode.Force);
            //     //     rb.AddForce(Vector3.up * 5.0f, ForceMode.Force);
            //     // }
            // }
            // if (Input.GetKey(KeyCode.A))
            // {
            //     rb.AddForce(Vector3.left * 3f, ForceMode.Acceleration);
            // }
            // if (Input.GetKey(KeyCode.D))
            // {
            //     rb.AddForce(Vector3.right * 3f, ForceMode.Acceleration);
            // }
            // if (factor == 0)
            // {
            //     factor = 0.01f;
            // }
            // Debug.Log(rb.maxLinearVelocity = 40f * factor);
            // rb.maxLinearVelocity = 40f / factor;
        }
        else
        {
            HookPoint();
            rb.maxLinearVelocity = 40f;
        }
        if (Input.GetMouseButtonDown(0) && !isSwing)
        {
            StartSwing();
            MouseDown();
            lastMousePos = Input.mousePosition; // 드래그 시작 위치
        }
        else if (Input.GetMouseButton(0)&& isSwing)
        {
            if (isDragging)
            {
                MouseDrag();
            }
        }
        else if (Input.GetMouseButtonUp(0) && isSwing)
        {
            EndSwing();
            isDragging = false;
        }
        DrawRope();
        lastMousePos = Input.mousePosition; // 매 프레임 갱신
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
        // --- 지민 ---
        // 스윙 시작 시 카메라 컨트롤러에 로프를 건 '위치(spot)'를 전달
        cameraController?.EnterSwingView(spot);
        // -------------------
        lr.positionCount = 2;                   // 라인 렌더러의 점 개수 설정
        lr.SetPosition(0, transform.position);  // 첫 번째 점을 플레이어 위치로 설정
        lr.SetPosition(1, hit.point);           // 두 번째 점을 레이캐스트 위치로 설정
        
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
        currentHangTime = 0f;
        lr.startWidth = 0.1f;
        rb.linearVelocity = new Vector3(rb.linearVelocity.x * 0.3f,
        rb.linearVelocity.y * 0.5f,
        rb.linearVelocity.z * 0.5f);
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
        if (dragDir.x < -0.1)
        {
            Debug.Log("좌");
            rb.AddForce(Vector3.forward * 2f, ForceMode.Acceleration);
            rb.AddForce(Vector3.left * 3f, ForceMode.Acceleration);
        }
        if (dragDir.x > 0.1)
        {
            Debug.Log("우");
            rb.AddForce(Vector3.forward * 2f, ForceMode.Acceleration);
            rb.AddForce(Vector3.right * 3f, ForceMode.Acceleration);
        }
        if (dragDir.y < 0)
        {
            Debug.Log("앞");
            rb.AddForce(Vector3.forward * 15f, ForceMode.Acceleration);
        }
        if (dragDir.y > 0)
        {
            Debug.Log("뒤");
            rb.AddForce(Vector3.back * 5f, ForceMode.Acceleration);
        }

        dragStartPos = currentPos; // 기준점 갱신 (연속 드래그 반영)
    }

        
}