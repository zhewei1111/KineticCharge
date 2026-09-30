using UnityEngine;

public class TargetSpawner : MonoBehaviour
{
    // 目標預製物與所有目標的共用中心點。
    public GameObject targetPrefab;
    public Transform centerZone;

    [Header("目標距離設定")]
    public float stepDistanceX = 2.5f;
    public float stepDistanceY = 2.5f;

    [Range(0.5f, 1f)]
    // 斜角目標稍微往內縮，避免玩家必須走到 Kinect 偵測範圍的極限角落。
    public float diagonalDistanceMultiplier = 0.82f;

    [Header("隨機目標設定")]
    [Tooltip("同一方向最多連續出現幾次")]
    [Min(1)]
    public int maxSameDirectionInARow = 2;

    // 場上目前唯一可碰觸的目標物。
    private GameObject currentTarget;
    private int previousIndex = -1;
    private int sameDirectionCount = 0;

    // 提供 PlayerController 讀取目前目標與中心座標，避免它直接操作生成器內部資料。
    public GameObject CurrentTarget => currentTarget;
    public Vector3 CurrentTargetPosition => currentTarget != null ? currentTarget.transform.position : centerZone.position;
    public Vector3 CenterPosition => centerZone != null ? centerZone.position : Vector3.zero;

    // 目前目標的方向、顯示名稱與陣列索引。
    public Vector2 currentDirection;
    public string currentStepType;
    public int currentIndex;

    public void SpawnTarget(bool[] completed)
    {
        // 依尚未完成的方向隨機生成一個新目標；限時模式會傳入全 false，讓八方向都可重複出現。
        if (targetPrefab == null || centerZone == null)
        {
            Debug.LogError("TargetSpawner requires both targetPrefab and centerZone.");
            return;
        }

        if (currentTarget != null)
            Destroy(currentTarget);

        // 八方向目標：上、下、左、右，以及四個斜角方向。
        Vector2[] directions = new Vector2[]
        {
            new Vector2(0, 1),
            new Vector2(0, -1),
            new Vector2(-1, 0),
            new Vector2(1, 0),
            new Vector2(-1, 1),
            new Vector2(1, 1),
            new Vector2(-1, -1),
            new Vector2(1, -1)
        };

        string[] stepNames = new string[]
        {
            "前",
            "後",
            "左",
            "右",
            "左前",
            "右前",
            "左後",
            "右後"
        };

        int[] candidates = new int[directions.Length];
        int candidateCount = 0;
        bool blockPreviousDirection = sameDirectionCount >= maxSameDirectionInARow;

        // 先建立可選方向清單，限時模式下八個方向都可重複，但同方向最多連續兩次。
        for (int i = 0; i < directions.Length; i++)
        {
            if (completed[i] || (blockPreviousDirection && i == previousIndex))
                continue;

            candidates[candidateCount] = i;
            candidateCount++;
        }

        // 若舊的非重複模式只剩被排除方向，仍允許它出現，避免沒有目標可生成。
        if (candidateCount == 0)
        {
            for (int i = 0; i < directions.Length; i++)
            {
                if (!completed[i])
                {
                    candidates[candidateCount] = i;
                    candidateCount++;
                }
            }
        }

        if (candidateCount == 0)
        {
            Debug.LogWarning("TargetSpawner has no available directions.");
            return;
        }

        int index = candidates[Random.Range(0, candidateCount)];

        if (index == previousIndex)
            sameDirectionCount++;
        else
        {
            previousIndex = index;
            sameDirectionCount = 1;
        }

        currentIndex = index;
        currentDirection = directions[index];
        currentStepType = stepNames[index];

        // 斜向目標依倍率內縮，降低走到 Kinect 偵測邊界的機率。
        bool isDiagonal = currentDirection.x != 0f && currentDirection.y != 0f;
        float distanceMultiplier = isDiagonal ? diagonalDistanceMultiplier : 1f;

        float finalX = currentDirection.x * stepDistanceX * distanceMultiplier;
        float finalY = currentDirection.y * stepDistanceY * distanceMultiplier;

        Vector3 pos = centerZone.position + new Vector3(finalX, finalY, 0);
        currentTarget = Instantiate(targetPrefab, pos, Quaternion.identity);
    }
}
