using JetBrains.Rider.Unity.Editor;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Burst.Intrinsics;
using UnityEngine;
using UnityEngine.InputSystem;

public class GraplingHook : MonoBehaviour
{
    public LayerMask layerMask;
    LineRenderer lr;
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

    // --- 지민 ---
    [Header("Refs")]
    [SerializeField] PlayerController playerController;
    [SerializeField] PlayerCameraController cameraController;
    Vector3 lastMousePos;
    
    void Start()
    {
        lr = GetComponent<LineRenderer>();
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
            lr.startWidth = currentWidth;
            lr.endWidth = currentWidth;
            if (currentHangTime >= hangingTime)
            {
                EndSwing();
                currentHangTime = 0f;
                lr.startWidth = 0.1f;
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
            playerController.LockRotation();
            cameraController.EnterSwingView();
        }
        catch
        {
            
        }
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
        try
        {
            playerController.UnlockRotation();
            cameraController.ExitSwingView();
        }
        catch
        {
            
        }
        currentHangTime = 0f;
        lr.startWidth = 0.1f;
        rb.linearVelocity = new Vector3(rb.linearVelocity.x * 0.3f,
        rb.linearVelocity.y,
        rb.linearVelocity.z);
        isSwing = false;
        lr.positionCount = 0;   // 라인 렌더러의 점 개수를 0으로 설정하여 선을 지움
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
            rb.AddForce(Vector3.back * dragSpeed/3, ForceMode.Impulse);
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
}