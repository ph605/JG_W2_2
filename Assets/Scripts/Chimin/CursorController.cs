using UnityEngine;

public class CursorController : MonoBehaviour
{
    private void Start()
    {
        // 게임 시작 시 마우스 커서 숨기기
        LockCursor();
    }

    // 마우스 커서 숨기기
    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked; // 커서 잠금
        Cursor.visible = false;                   // 커서 숨김
    }

    // 마우스 커서 보이게 하기
    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;   // 커서 잠금 해제
        Cursor.visible = true;                    // 커서 표시
    }
}
