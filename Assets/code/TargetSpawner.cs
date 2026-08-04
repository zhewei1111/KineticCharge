using UnityEngine;

public class TargetSpawner : MonoBehaviour
{
    public GameObject targetPrefab;
    public Transform centerZone;

    [Header("目標距離設定")]
    public float stepDistanceX = 2.5f;
    public float stepDistanceY = 2.5f;

    [Range(0.5f, 1f)]
    // 斜角目標稍微往內縮，避免玩家必須走到 Kinect 偵測範圍的極限角落。
    public float diagonalDistanceMultiplier = 0.82f;

    private GameObject currentTarget;

    public GameObject CurrentTarget => currentTarget;
    public Vector3 CurrentTargetPosition => currentTarget != null ? currentTarget.transform.position : centerZone.position;
    public Vector3 CenterPosition => centerZone != null ? centerZone.position : Vector3.zero;

    public Vector2 currentDirection;
    public string currentStepType;
    public int currentIndex;

    public void SpawnTarget(bool[] completed)
    {
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

        int index;
        int attempts = 0;

        // 隨機挑選還沒完成的方向，避免同一組訓練重複出現已完成目標。
        do
        {
            index = Random.Range(0, directions.Length);
            attempts++;

            if (attempts > 50)
                break;
        } while (completed[index]);

        currentIndex = index;
        currentDirection = directions[index];
        currentStepType = stepNames[index];

        bool isDiagonal = currentDirection.x != 0f && currentDirection.y != 0f;
        float distanceMultiplier = isDiagonal ? diagonalDistanceMultiplier : 1f;

        float finalX = currentDirection.x * stepDistanceX * distanceMultiplier;
        float finalY = currentDirection.y * stepDistanceY * distanceMultiplier;

        Vector3 pos = centerZone.position + new Vector3(finalX, finalY, 0);
        currentTarget = Instantiate(targetPrefab, pos, Quaternion.identity);
    }
}
