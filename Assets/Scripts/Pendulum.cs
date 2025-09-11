using UnityEngine;

public class ArcadeSpiderGrapple_NoRB : MonoBehaviour
{
    public Camera cam;
    public LayerMask grappleMask;
    public float hookRange = 50f;
    public float pullSpeed = 10f;     // 줄 당기는 속도
    public float swingSpeed = 8f;     // 스윙 속도
    public float minRopeLength = 2f;  // 최소 줄 길이
    public LineRenderer lr;

    private Vector3 hookPoint;
    private bool isGrappling = false;
    private float ropeLength;
    private Vector3 velocity;

    void Update()
    {
        // 훅 발사
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, hookRange, grappleMask))
            {
                hookPoint = hit.point;
                ropeLength = Vector3.Distance(transform.position, hookPoint);
                isGrappling = true;
                velocity = Vector3.zero; // 초기화
            }
        }

        // 훅 해제
        if (Input.GetMouseButtonUp(0))
        {
            isGrappling = false;
            lr.positionCount = 0;
        }

        // 줄 당기기 (우클릭)
        if (isGrappling && Input.GetMouseButton(1))
        {
            ropeLength -= pullSpeed * Time.deltaTime;
            ropeLength = Mathf.Max(minRopeLength, ropeLength);
        }
    }

    void FixedUpdate()
    {
        if (isGrappling)
        {
            Vector3 pos = transform.position;
            Vector3 dirToHook = (hookPoint - pos);
            float dist = dirToHook.magnitude;

            // === 1) 중력 적용 ===
            velocity += Physics.gravity * Time.fixedDeltaTime;

            // === 2) 입력에 따른 스윙 ===
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            Vector3 inputDir = (cam.transform.right * h + cam.transform.forward * v).normalized;
            velocity += inputDir * swingSpeed * Time.fixedDeltaTime;

            // === 3) 위치 업데이트 ===
            pos += velocity * Time.fixedDeltaTime;

            // === 4) 줄 길이 유지 (hookPoint와 일정 거리 유지) ===
            Vector3 ropeDir = (pos - hookPoint).normalized;
            pos = hookPoint + ropeDir * Mathf.Min(ropeLength, (pos - hookPoint).magnitude);

            transform.position = pos;

            // === 5) 로프 라인 ===
            lr.positionCount = 2;
            lr.SetPosition(0, hookPoint);
            lr.SetPosition(1, pos);
        }
        else
        {
            // 그래플링 안할 때 → 그냥 속도 반영 (중력 포함)
            velocity += (Physics.gravity * Time.fixedDeltaTime) /10;
            transform.position += velocity * Time.fixedDeltaTime;
        }
    }
}
