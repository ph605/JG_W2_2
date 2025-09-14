using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class ClingInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private PlayerController playerController;

    [Header("Holding Filter")]
    [Tooltip("���� �� �ִ� ���̾� �̸�")]
    [SerializeField] private string holdingLayerName = "Holding";
    [SerializeField] private float rayDistance = 80f;

    [Header("Cling Settings")]
    [Tooltip("ǥ�鿡�� ��¦ ��� ���� �Ÿ�")]
    [SerializeField] private float clingOffset = 0.12f;
    [Tooltip("ǥ�� ���� ������ �ٶ󺸵��� ����")]
    [SerializeField] private bool alignToSurface = true;
    [Tooltip("�پ��ִ� ���� Ÿ���� �ڽ����� ���̱�(�����̴� �÷��� ����)")]
    [SerializeField] private bool parentWhileHolding = true;

    [Header("Velocity Handling")]
    [Tooltip("�ٴ� ���� �ӵ��� 0���� ����")]
    [SerializeField] private bool zeroVelocityOnBegin = true;
    [Tooltip("���� �� �ٱ� �� ���� �ӵ��� ����")]
    [SerializeField] private bool preserveHorizontalVelocityOnRelease = false;
    [Tooltip("���� �� ���� ��¦ ���ִ� ����")]
    [SerializeField] private float releaseUpBoost = 0f;

    // ���� ����
    private Rigidbody rb;
    private bool isHolding = false;
    private Transform holdTarget;
    private Vector3 holdLocalPos;
    private Quaternion holdLocalRot;
    private Vector3 preHoldVelocity;
    private int holdingLayer;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (cam == null) cam = Camera.main;
        if (playerController == null) playerController = GetComponent<PlayerController>();

        holdingLayer = LayerMask.NameToLayer(holdingLayerName);
        if (holdingLayer == -1)
            Debug.LogWarning($"[ClingInteractor] '{holdingLayerName}' ���̾ ã�� ���߽��ϴ�. ���̾ �����ϰ� �����ϼ���.");
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (!isHolding)
        {
            if (mouse.leftButton.wasPressedThisFrame)
                TryBeginHold();
        }
        else
        {
            if (mouse.leftButton.isPressed)
            {
                MaintainHold();
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                EndHold();
            }
        }
    }

    void TryBeginHold()
    {
        if (cam == null) return;
        if (holdingLayer == -1) return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        int mask = 1 << holdingLayer;

        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, mask, QueryTriggerInteraction.Collide))
        {
            BeginHold(hit);
        }
    }

    void BeginHold(RaycastHit hit)
    {
        // ����/����Ʈ�� �پ����� �� ������ �����ϰ� ����
        var sj = GetComponent<SpringJoint>();
        if (sj != null) Destroy(sj);

        // �ٱ� �� �ӵ� ����(���� �� ���� �ɼǿ�)
        preHoldVelocity = rb.linearVelocity;

        // ��ġ/ȸ�� ���
        Vector3 normal = hit.normal;
        Vector3 anchorWorldPos = hit.point + normal * clingOffset;
        Quaternion anchorWorldRot = alignToSurface
            ? Quaternion.LookRotation(-normal, Vector3.up)
            : transform.rotation;

        // ���� ���� �� ����
        if (zeroVelocityOnBegin)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        rb.isKinematic = true;
        transform.position = anchorWorldPos;
        transform.rotation = anchorWorldRot;

        // �θ� ��� �� �� ���� ����� ���� ���� ����
        holdTarget = hit.collider.transform;
        holdLocalPos = holdTarget.InverseTransformPoint(anchorWorldPos);
        holdLocalRot = Quaternion.Inverse(holdTarget.rotation) * anchorWorldRot;

        if (parentWhileHolding && holdTarget != null)
            transform.SetParent(holdTarget, true);

        // �÷��̾� ���� ����
        if (playerController != null)
        {
            playerController.IsClinging = true;
            playerController.LockController();
            playerController.LockRotation();
        }

        isHolding = true;
        // Debug.Log("[ClingInteractor] Hold begin");
    }

    void MaintainHold()
    {
        if (parentWhileHolding || holdTarget == null) return;

        // �θ� ���� �ʴ� ���, Ÿ�� ��ȭ�� ������ ��ġ/ȸ�� ����
        Vector3 worldPos = holdTarget.TransformPoint(holdLocalPos);
        Quaternion worldRot = holdTarget.rotation * holdLocalRot;

        transform.position = worldPos;
        transform.rotation = worldRot;
    }

    void EndHold()
    {
        if (!isHolding) return;

        if (parentWhileHolding)
            transform.SetParent(null, true);

        rb.isKinematic = false;

        Vector3 v = rb.linearVelocity;
        if (preserveHorizontalVelocityOnRelease)
        {
            Vector3 horiz = new Vector3(preHoldVelocity.x, 0f, preHoldVelocity.z);
            v.x = horiz.x;
            v.z = horiz.z;
        }
        if (releaseUpBoost > 0f)
        {
            v.y = Mathf.Max(v.y, releaseUpBoost);
        }
        rb.linearVelocity = v;

        if (playerController != null)
        {
            playerController.IsClinging = false;
            playerController.UnlockRotation();
            playerController.UnlockController();
        }

        isHolding = false;
        holdTarget = null;
        // Debug.Log("[ClingInteractor] Hold end");
    }
}
