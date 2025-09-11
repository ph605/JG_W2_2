using System.Collections;
using System.Collections.Generic;
using JetBrains.Rider.Unity.Editor;
using UnityEngine;

public class GrapplingHook : MonoBehaviour
{
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

    Vector3 lastMousePos;
    void Start()
    {
        lr = GetComponent<LineRenderer>();
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        HookPoint();

        if (Input.GetMouseButtonDown(0))
        {
            StartSwing();
            lastMousePos = Input.mousePosition; // 드래그 시작 위치
        }
        else if (Input.GetMouseButtonUp(0))
        {
            EndSwing();
        }

        if (isSwing)
        {
            UpdateSpringForceByDrag();
        }

        DrawRope();

        lastMousePos = Input.mousePosition; // 매 프레임 갱신
    }
    void UpdateSpringForceByDrag()
{
    Vector3 mouseDelta = Input.mousePosition - lastMousePos;
    //x,y만 사용 Y축 - 일때 마우스 당기는거
    
    // if (mouseDelta.sqrMagnitude < 0.01f) return; // 거의 움직이지 않으면 무시

    // 캐릭터 → 훅 지점 방향 (월드 공간)
    Vector3 hookDir = (spot - transform.position).normalized;

    // 마우스 드래그 방향 → 월드 공간으로 변환 (카메라 기준)
    // Vector3 camRight = cam.transform.right;
    // Vector3 camUp = cam.transform.up;
    // Vector3 dragWorld = (camRight * mouseDelta.x + camUp * mouseDelta.y).normalized;

    // 내적 > 0이면 앞으로, < 0이면 뒤쪽
    // float dot = Vector3.Dot(dragWorld, hookDir);

    if (mouseDelta.y < 0) // 뒤로 드래그할 때만 힘 적용
    {
        Debug.Log("Dragging");
            // float dragMagnitude = mouseDelta.magnitude;
            // float mappedValue = Mathf.Clamp(mouseDelta.y * 2f, 5f, 20f);
            rb.linearDamping = 0;
            rb.AddForce(Vector3.forward * 0.02f, ForceMode.Impulse);
            
        if (sj != null)
            {
                // sj.spring = mappedValue;
                // sj.damper = mappedValue;
            }
    }
    else
    {
            if (sj != null)
            {
                // rb.AddForce(Vector3.down * 0.3f, ForceMode.Impulse);
                rb.linearDamping += 0.002f;
                // sj.spring = 5f;
                // sj.damper = 5f;
            }
    }
}


    void HookPoint()
    {
        
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Physics.Raycast(ray, out RaycastHit rayCastHit, rayDistance,layerMask);

        RaycastHit sphereCastHit;
        Physics.SphereCast(cam.transform.position, 4, cam.transform.forward, out sphereCastHit, rayDistance,layerMask);

        Vector3 realHitPoint;

        if (rayCastHit.point != Vector3.zero)
        {
            realHitPoint = rayCastHit.point;
        }
        else if (sphereCastHit.point != Vector3.zero)
        {
            realHitPoint = sphereCastHit.point;
        }
        aim.transform.position = rayCastHit.point;

        hit = rayCastHit.point == Vector3.zero ? sphereCastHit : rayCastHit;
    }

    void StartSwing()
    {
        if (hit.point == Vector3.zero) return;

        isSwing = true;

        spot = hit.point;   // 로프를 연결할 지점 설정

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
