using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 15f; // 회전 속도를 조금 더 높여 반응성을 개선

    [Header("Dependencies")]
    [SerializeField] private Transform cameraTransform;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (cameraTransform == null)
        {
            Debug.LogError("PlayerMovement: Camera Transform이 할당되지 않았습니다!");
        }
    }

    void Update()
    {
        // 입력 받기
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");

        // 카메라 방향 기준으로 이동 방향 계산
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0;
        camRight.y = 0;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDirection = (camForward * verticalInput + camRight * horizontalInput).normalized;

        // 이동 및 회전 적용
        if (moveDirection != Vector3.zero)
        {
            // 리지드바디 속도를 이용해 이동 (물리 업데이트인 FixedUpdate에서 처리하는 것이 더 안정적일 수 있으나,
            // 이 방식이 더 직관적이고 반응성이 좋을 수 있습니다)
            rb.linearVelocity = new Vector3(moveDirection.x * moveSpeed, rb.linearVelocity.y, moveDirection.z * moveSpeed);

            // 이동 방향으로 캐릭터 회전
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        else
        {
            // 입력이 없으면 수평 속도 0으로 설정
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        }
    }
}