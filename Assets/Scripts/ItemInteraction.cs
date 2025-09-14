using UnityEngine;

public class ItemInteraction : MonoBehaviour
{
    public int score = 10;
    public float rotateSpeed = 60f;
    public float floatAmplitude = 0.25f;
    public float floatFrequency = 1f;

    private Vector3 startPos;

    private bool isOnce = true;

    // Update is called once per frame
    void Update()
    {
        RotateItem();
        FloatingItem();
    }

    public void SetStartPos(Vector3 pos)
    {
        startPos = pos;
    }

    private void RotateItem()
    {
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime, Space.World);
    }

    private void FloatingItem()
    {
        transform.position = startPos + Vector3.up * Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
    }

    // 플레이어와 충돌 시, 점수 추가
    private void OnTriggerEnter(Collider other)
    {
        if (isOnce == false)
            return;

        if (other.gameObject.CompareTag("Player"))
        {
            isOnce = false;
            InGameUIManager.instance.AddScore(score);

            int dataID = gameObject.GetComponent<PooledObjectInfo>().dataID;
            Debug.Log(dataID);
            MapGenerator.instance.ItemSetActive(dataID, false);
            gameObject.SetActive(false);
        }
    }
}
