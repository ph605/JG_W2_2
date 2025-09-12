using UnityEngine;

using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.Events;   // (선택) onDie 이벤트 쓰려면

public class Player : MonoBehaviour
{
    [Header("Shield")]
    [SerializeField] GameObject shieldPrefab;       // 장착할 쉴드 프리팹
    [SerializeField] Transform shieldAttachPoint;   // 붙일 기준(없으면 Player 중심)
    GameObject activeShield;                        // 현재 장착된 쉴드

    // 클래스 내부(필드들 근처)에 추가
    [Header("Death/Respawn")]
    [SerializeField] bool autoReloadOnDeath = true;   // 죽으면 자동으로 현재 씬 리로드
    [SerializeField] float reloadDelay = 0.5f;        // 리로드 지연 시간

    [Header("Events (Optional)")]
    public UnityEvent onDie; // 외부에서 연결해 쓸 수 있는 사망 이벤트(선택)


    Rigidbody rb;
    bool isDying = false;

    public bool HasShield => activeShield != null;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        isDying = false; // 리스폰 시 초기화
    }

    // 비트리거 콜라이더(일반 충돌)
    void OnCollisionEnter(Collision collision)
    {
        var col = collision.collider;

        if (col.CompareTag("Shield"))
        {
            EquipShield();
            Destroy(col.gameObject);
            return;
        }

        if (col.CompareTag("Die"))
        {
            Die();
            return;
        }
    }

    // 트리거 콜라이더(Trigger = On)
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Shield"))
        {
            EquipShield();
            Destroy(other.gameObject);
            return;
        }

        if (other.CompareTag("Die"))
        {
            Die();
            return;
        }
    }

    public void EquipShield()
    {
        if (shieldPrefab == null) return;
        if (activeShield != null) return; // 중복 착용 불가

        Transform attach = shieldAttachPoint != null ? shieldAttachPoint : transform;

        // 프리팹 스케일 유지: 부모에 붙이되 scale은 건드리지 않음
        activeShield = Instantiate(shieldPrefab, attach.position, attach.rotation, attach);
        activeShield.transform.localPosition = Vector3.zero;
        activeShield.transform.localRotation = Quaternion.identity;
        // activeShield.transform.localScale = Vector3.one; // 강제하지 않음
    }

    // 필요 시 외부에서 한 번 소모(예: 특정 함정에 맞았을 때)
    public bool ConsumeShield()
    {
        if (activeShield == null) return false;

        Destroy(activeShield);
        activeShield = null;
        return true;
    }

    public void RemoveShield()
    {
        if (activeShield != null)
        {
            Destroy(activeShield);
            activeShield = null;
        }
    }

    public void Die()
    {
        if (isDying) return;
        isDying = true;

        if (rb != null)
        {
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;  // 프로젝트 컨벤션 유지
            rb.angularVelocity = Vector3.zero;
        }

        // (선택) 외부가 구독했다면 먼저 알림
        onDie?.Invoke();

        // GameManager 없이도 동작: 현재 씬을 자체 리로드
        if (autoReloadOnDeath)
            StartCoroutine(ReloadSceneAfterDelay());
    }

    IEnumerator ReloadSceneAfterDelay()
    {
        yield return new WaitForSeconds(reloadDelay);
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }

}
