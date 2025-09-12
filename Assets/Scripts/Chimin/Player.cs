using UnityEngine;

using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.Events;   // (����) onDie �̺�Ʈ ������

public class Player : MonoBehaviour
{
    [Header("Shield")]
    [SerializeField] GameObject shieldPrefab;       // ������ ���� ������
    [SerializeField] Transform shieldAttachPoint;   // ���� ����(������ Player �߽�)
    GameObject activeShield;                        // ���� ������ ����

    // Ŭ���� ����(�ʵ�� ��ó)�� �߰�
    [Header("Death/Respawn")]
    [SerializeField] bool autoReloadOnDeath = true;   // ������ �ڵ����� ���� �� ���ε�
    [SerializeField] float reloadDelay = 0.5f;        // ���ε� ���� �ð�

    [Header("Events (Optional)")]
    public UnityEvent onDie; // �ܺο��� ������ �� �� �ִ� ��� �̺�Ʈ(����)


    Rigidbody rb;
    bool isDying = false;

    public bool HasShield => activeShield != null;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        isDying = false; // ������ �� �ʱ�ȭ
    }

    // ��Ʈ���� �ݶ��̴�(�Ϲ� �浹)
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

    // Ʈ���� �ݶ��̴�(Trigger = On)
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
        if (activeShield != null) return; // �ߺ� ���� �Ұ�

        Transform attach = shieldAttachPoint != null ? shieldAttachPoint : transform;

        // ������ ������ ����: �θ� ���̵� scale�� �ǵ帮�� ����
        activeShield = Instantiate(shieldPrefab, attach.position, attach.rotation, attach);
        activeShield.transform.localPosition = Vector3.zero;
        activeShield.transform.localRotation = Quaternion.identity;
        // activeShield.transform.localScale = Vector3.one; // �������� ����
    }

    // �ʿ� �� �ܺο��� �� �� �Ҹ�(��: Ư�� ������ �¾��� ��)
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
            rb.linearVelocity = Vector3.zero;  // ������Ʈ ������ ����
            rb.angularVelocity = Vector3.zero;
        }

        // (����) �ܺΰ� �����ߴٸ� ���� �˸�
        onDie?.Invoke();

        // // GameManager ���̵� ����: ���� ���� ��ü ���ε�
        // if (autoReloadOnDeath)
        //     StartCoroutine(ReloadSceneAfterDelay());
    }

    // IEnumerator ReloadSceneAfterDelay()
    // {
    //     yield return new WaitForSeconds(reloadDelay);
    //     var scene = SceneManager.GetActiveScene();
    //     SceneManager.LoadScene(scene.name);
    // }

}
