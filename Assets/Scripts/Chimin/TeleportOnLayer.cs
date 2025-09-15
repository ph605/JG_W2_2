using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class TeleportOnLayer : MonoBehaviour
{
    [Header("Layer Names")]
    [SerializeField] string tutorial1LayerName = "Tutorial1";
    [SerializeField] string tutorial2LayerName = "Tutorial2";
    [SerializeField] string tutorial3LayerName = "Tutorial3";

    [Header("Teleport Targets (World)")]
    [SerializeField] Vector3 tutorial1Target = new Vector3(0f, 1.5f, 0f);
    [SerializeField] Vector3 tutorial2Target = new Vector3(0f, 1.5f, 180f);
    [SerializeField] Vector3 tutorial3Target = new Vector3(0f, 1.5f, 365f);

    int layer1, layer2, layer3;
    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        layer1 = LayerMask.NameToLayer(tutorial1LayerName);
        layer2 = LayerMask.NameToLayer(tutorial2LayerName);
        layer3 = LayerMask.NameToLayer(tutorial3LayerName);
    }

    // 충돌형 콜라이더
    void OnCollisionEnter(Collision collision)
    {
        TryTeleport(collision.gameObject.layer);
    }

    // 트리거형 콜라이더
    void OnTriggerEnter(Collider other)
    {
        TryTeleport(other.gameObject.layer);
    }

    void TryTeleport(int hitLayer)
    {
        if (hitLayer == layer1)
            Teleport(tutorial1Target);
        else if (hitLayer == layer2)
            Teleport(tutorial2Target);
        else if (hitLayer == layer3)
            Teleport(tutorial3Target);
    }

    void Teleport(Vector3 worldPos)
    {
        // 이동 직전 속도 제거
        rb.linearVelocity = Vector3.zero;    // 환경에 따라 rb.velocity 사용
        rb.angularVelocity = Vector3.zero;

        // 텔레포트
        rb.position = worldPos;
        transform.position = worldPos;       // 보정
    }
}
