using UnityEngine;

public class CursorController : MonoBehaviour
{
    public Canvas canvas;              // 크로스헤어가 속한 캔버스
    public RectTransform crosshairUI;  // 크로스헤어 UI (Image 등)
    private void Start()
    {
        // ���� ���� �� ���콺 Ŀ�� �����
        LockCursor();
    }
    void Update()
    {
        // Vector2 localPoint;
        // // 마우스 스크린 좌표를 캔버스 로컬 좌표로 변환
        // RectTransformUtility.ScreenPointToLocalPointInRectangle(
        //     canvas.transform as RectTransform,
        //     Input.mousePosition,
        //     canvas.worldCamera,
        //     out localPoint
        // );
        // // 크로스헤어 위치를 마우스 위치로 업데이트
        // crosshairUI.localPosition = localPoint;
    }

    // ���콺 Ŀ�� �����
    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked; // Ŀ�� ���
        Cursor.visible = false;                   // Ŀ�� ����
    }

    // ���콺 Ŀ�� ���̰� �ϱ�
    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;   // Ŀ�� ��� ����
        Cursor.visible = true;                    // Ŀ�� ǥ��
    }
}
