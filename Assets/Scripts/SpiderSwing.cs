using UnityEngine;

public class SpiderSwingPendulum : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public LineRenderer lineRenderer;

    [Header("Swing Settings")]
    public float maxDistance = 50f;
    public float gravity = 9f;           // 진자 운동용 중력
    public float swingForce = 0.5f;      // 마우스 드래그 힘
    public float ropeDamping = 0.995f;    // 감쇠 거의 없음
    public float launchBoost = 20f;      // 줄 놓을 때 앞으로 튀는 힘

    private bool isSwinging = false;
    private Vector3 anchorPoint;
    private float ropeLength;
    private Vector3 velocity;

    private Vector3 lastMousePos;

    void Start()
    {
        if (cam == null) cam = Camera.main;

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
            lineRenderer.positionCount = 2;
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            TryAttachRope();
            lastMousePos = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(0))
        {
            DetachRope();
        }

        if (isSwinging)
        {
            ApplyPendulumGravity();
            ApplyMouseDragForce();
            UpdatePosition();
            DrawRope();

            lastMousePos = Input.mousePosition;
        }
        else
        {
            // 자유 낙하
            velocity.y -= gravity * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
        }
    }

    void TryAttachRope()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            anchorPoint = hit.point;
            ropeLength = Vector3.Distance(transform.position, anchorPoint);
            isSwinging = true;
            velocity = Vector3.zero;

            if (lineRenderer != null)
                lineRenderer.enabled = true;
        }
    }

    void DetachRope()
    {
        if (!isSwinging) return;

        // 현재 속도 유지 + 앞으로 튕김
        Vector3 dir = (transform.position - anchorPoint).normalized;
        Vector3 tangent = Vector3.Cross(Vector3.up, dir).normalized;
        velocity += dir * launchBoost + tangent * launchBoost * 0.5f;

        isSwinging = false;
        if (lineRenderer != null)
            lineRenderer.enabled = false;
    }

    void ApplyPendulumGravity()
    {
        Vector3 dir = (transform.position - anchorPoint).normalized;

        // 중력 적용 → 줄 방향 성분 제거, 접선 방향 힘만 남김
        Vector3 gravityForce = Vector3.down * gravity * Time.deltaTime;
        Vector3 tangentialForce = gravityForce - Vector3.Dot(gravityForce, dir) * dir;

        velocity += tangentialForce;
    }

    void ApplyMouseDragForce()
    {
        Vector3 mouseDelta = Input.mousePosition - lastMousePos;

        Vector3 camRight = cam.transform.right;
        Vector3 camUp = cam.transform.up;

        Vector3 dragWorld = (camRight * mouseDelta.x + camUp * mouseDelta.y).normalized;

        Vector3 dir = (transform.position - anchorPoint).normalized;
        dragWorld -= Vector3.Dot(dragWorld, dir) * dir;

        velocity += dragWorld * swingForce;
    }

    void UpdatePosition()
    {
        Vector3 dir = (transform.position - anchorPoint).normalized;

        // 감쇠
        velocity *= ropeDamping;

        // 위치 갱신
        transform.position += velocity * Time.deltaTime;

        // 줄 길이 유지
        Vector3 offset = transform.position - anchorPoint;
        transform.position = anchorPoint + offset.normalized * ropeLength;

        // 줄 방향 속도 제거
        velocity -= Vector3.Dot(velocity, dir) * dir;
    }

    void DrawRope()
    {
        if (lineRenderer != null)
        {
            lineRenderer.SetPosition(0, anchorPoint);
            lineRenderer.SetPosition(1, transform.position);
        }
    }
}
