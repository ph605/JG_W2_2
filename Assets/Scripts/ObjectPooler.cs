using System.Collections.Generic;
using UnityEngine;

public class ObjectPooler : MonoBehaviour
{
    // 싱글턴 패턴: 어디서든 쉽게 접근 가능하도록 함
    public static ObjectPooler Instance;

    [System.Serializable]
    public class Pool
    {
        public GameObject prefab;
        public int size;
    }

    public List<Pool> pools;
    public Dictionary<GameObject, Queue<GameObject>> poolDictionary = new Dictionary<GameObject, Queue<GameObject>>();

    void Awake()
    {
        // 1. 이미 Instance가 존재하고, 그것이 현재 이 오브젝트가 아니라면
        if (Instance != null && Instance != this)
        {
            // 이 오브젝트는 중복이므로 파괴하고, 원래의 Instance를 계속 사용합니다.
            Destroy(gameObject);
            return; // 아래 코드를 실행하지 않고 즉시 종료
        }

        // 2. Instance가 아직 없다면, 이 오브젝트를 유일한 Instance로 지정합니다.
        Instance = this;
        InitPool();
        // 3. 이 오브젝트를 씬이 바뀌어도 파괴되지 않도록 설정합니다.
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {

    }

    // 처음에 Pooler에 객체 생성 및 추가
    void InitPool()
    {
        foreach (Pool pool in pools)
        {
            Queue<GameObject> objectPool = new Queue<GameObject>();

            for (int i = 0; i < pool.size; ++i)
            {
                GameObject obj = Instantiate(pool.prefab);
                obj.transform.SetParent(transform);

                PooledObjectInfo info = obj.AddComponent<PooledObjectInfo>();
                info.OriginalPrefab = pool.prefab;

                obj.SetActive(false);
                objectPool.Enqueue(obj);
            }
            poolDictionary.Add(pool.prefab, objectPool);
        }
    }

    public GameObject SpawnFromPool(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        // Debug.Log("SpawnFromPool " + prefab);
        if (poolDictionary.ContainsKey(prefab) == false)
        {
            // Debug.LogWarning("Pool with name " + prefab.name + " doesn't exist.");
            return null;
        }

        GameObject objectToSpawn;
        // 오브젝트 풀에 없는 경우, 새롭게 생성
        if (poolDictionary[prefab].Count == 0)
        {
            objectToSpawn = Instantiate(prefab);
            objectToSpawn.transform.SetParent(transform);
            PooledObjectInfo addInfo = objectToSpawn.AddComponent<PooledObjectInfo>();
            addInfo.OriginalPrefab = prefab;
        }
        // 오브젝트 풀에 있는 경우, 기존 오브젝트 재활용
        else
        {
            objectToSpawn = poolDictionary[prefab].Dequeue();
        }

        objectToSpawn.SetActive(true);
        objectToSpawn.transform.position = position;
        objectToSpawn.transform.rotation = rotation;

        return objectToSpawn;
    }

    // 반납 함수가 태그 대신 GameObject를 받도록 변경
    public void ReturnToPool(GameObject objectToReturn)
    {
        // 1. 반납할 오브젝트의 '출신 정보'를 확인
        PooledObjectInfo info = objectToReturn.GetComponent<PooledObjectInfo>();
        if (info == null)
        {
            // Debug.LogWarning("Trying to return an object that was not pooled: " + objectToReturn.name);
            Destroy(objectToReturn);
            return;
        }

        // 2. 출신 프리팹을 Key로 사용하여 올바른 풀을 찾음
        GameObject originalPrefab = info.OriginalPrefab;
        if (poolDictionary.ContainsKey(originalPrefab) == false)
        {
            // Debug.LogWarning("Pool for prefab " + originalPrefab.name + " doesn't exist.");
            Destroy(objectToReturn);
            return;
        }

        objectToReturn.transform.SetParent(transform);

        // 3. 오브젝트를 비활성화하고 올바른 풀에 반납
        info.dataID = 0;
        objectToReturn.SetActive(false);
        poolDictionary[originalPrefab].Enqueue(objectToReturn);
    }
}
