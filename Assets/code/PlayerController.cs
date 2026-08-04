using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("移動設定")]
    public int totalSets = 1;

    [Header("直線跟隨移動")]
    [Tooltip("人體前後移動到這個比例時，控制點會到達目標")]
    [Range(0.1f, 1f)]
    public float forwardBackwardTargetInputDistance = 0.7f;
    [Tooltip("人體左右移動到這個比例時，控制點會到達目標")]
    [Range(0.1f, 1f)]
    public float horizontalTargetInputDistance = 0.7f;
    [Tooltip("人體斜向移動到這個比例時，控制點會到達目標")]
    [Range(0.1f, 1f)]
    public float diagonalTargetInputDistance = 0.6f;
    [Tooltip("距離目標或中心多近時算完成")]
    public float reachDistance = 0.3f;

    public TMP_Text stepText;
    public TMP_Text progressText;
    public TMP_Text countText;
    public GameObject finishText;
    public GameObject restartButton;

    [Header("開始倒數設定")]
    public float startCountdownSeconds = 3f;

    [Header("音效設定")]
    public AudioClip targetHitSound;
    public AudioClip centerReturnSound;
    public AudioClip countdownTickSound;
    public AudioClip startSound;
    public AudioClip finishSound;

    private int completedSets = 0;
    private bool trainingOver = false;
    private bool gameStarted = false;
    private bool waitingForCenter = false;
    private TargetSpawner spawner;
    private AudioSource audioSource;

    private bool[] stepCompleted = new bool[8];
    private int completedDirections = 0;

    void Start()
    {
        ResolveCountText();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        spawner = FindFirstObjectByType<TargetSpawner>();
        if (spawner == null)
        {
            Debug.LogError("PlayerController could not find a TargetSpawner in the scene.");
            enabled = false;
            return;
        }

        for (int i = 0; i < stepCompleted.Length; i++)
        {
            stepCompleted[i] = false;
        }

        UpdateProgressUI();

        if (finishText != null)
            finishText.SetActive(false);

        if (restartButton != null)
            restartButton.SetActive(false);

        if (countText != null)
            countText.gameObject.SetActive(true);

        StartCoroutine(StartCountdown());
    }

    void Update()
    {
        if (trainingOver || !gameStarted)
            return;

        if (KinectManager.instance == null)
            return;

        Vector2 playerInput = KinectManager.instance.playerTargetPercent;
        FollowKinectOnTargetLine(playerInput);

        if (waitingForCenter)
        {
            if (Vector2.Distance(transform.position, spawner.CenterPosition) <= reachDistance)
                CompleteCenterReturn();

            return;
        }

        if (spawner.CurrentTarget != null &&
            Vector2.Distance(transform.position, spawner.CurrentTargetPosition) <= reachDistance)
        {
            CompleteCurrentTarget(spawner.CurrentTarget);
        }
    }

    IEnumerator StartCountdown()
    {
        float remainingTime = startCountdownSeconds;
        int lastShownSecond = -1;

        while (remainingTime > 0f)
        {
            int shownSecond = Mathf.CeilToInt(remainingTime);
            if (countText != null)
                countText.text = shownSecond.ToString();

            if (shownSecond != lastShownSecond)
            {
                PlaySound(countdownTickSound);
                lastShownSecond = shownSecond;
            }

            remainingTime -= Time.deltaTime;
            yield return null;
        }

        if (countText != null)
            countText.gameObject.SetActive(false);

        PlaySound(startSound);

        yield return new WaitForSeconds(0.3f);

        gameStarted = true;
        spawner.SpawnTarget(stepCompleted);
        UpdateStepUI();
    }

    void ResolveCountText()
    {
        if (countText != null)
            return;

        TMP_Text[] textComponents = FindObjectsByType<TMP_Text>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (TMP_Text textComponent in textComponents)
        {
            if (textComponent.gameObject.name == "CountText")
            {
                countText = textComponent;
                return;
            }
        }

        Debug.LogWarning("PlayerController could not find CountText.");
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 目標與中心統一由 Update 的距離判定結算，避免碰撞事件重複觸發。
    }

    void FollowKinectOnTargetLine(Vector2 playerInput)
    {
        bool isDiagonal = spawner.currentDirection.x != 0f && spawner.currentDirection.y != 0f;
        float distanceMultiplier = isDiagonal ? spawner.diagonalDistanceMultiplier : 1f;
        Vector2 targetOffset = new Vector2(
            spawner.currentDirection.x * spawner.stepDistanceX * distanceMultiplier,
            spawner.currentDirection.y * spawner.stepDistanceY * distanceMultiplier
        );

        Vector2 targetDirection = targetOffset.normalized;
        float targetDistance = targetOffset.magnitude;
        float inputDistance = isDiagonal
            ? diagonalTargetInputDistance
            : spawner.currentDirection.x != 0f
                ? horizontalTargetInputDistance
                : forwardBackwardTargetInputDistance;

        // 只使用目前目標方向的投影，控制點因此始終沿著同一條直線移動。
        float inputProgress = Mathf.Clamp01(Vector2.Dot(playerInput, targetDirection) / inputDistance);
        float desiredDistance = targetDistance * inputProgress;
        Vector2 centerPosition = spawner.CenterPosition;
        Vector2 nextPosition = centerPosition + targetDirection * desiredDistance;
        transform.position = new Vector3(nextPosition.x, nextPosition.y, transform.position.z);
    }

    void CompleteCurrentTarget(GameObject targetObject)
    {
        if (waitingForCenter || trainingOver)
            return;

        int index = spawner.currentIndex;

        // 每一組訓練中，每個方向只計算一次完成。
        if (!stepCompleted[index])
        {
            stepCompleted[index] = true;
            completedDirections++;
        }

        if (targetObject != null)
        {
            transform.position = targetObject.transform.position;
            Destroy(targetObject);
        }

        PlaySound(targetHitSound);

        waitingForCenter = true;
        UpdateStepUI();
    }

    void CompleteCenterReturn()
    {
        if (!waitingForCenter || trainingOver)
            return;

        waitingForCenter = false;
        transform.position = spawner.CenterPosition;
        PlaySound(centerReturnSound);

        // 八個方向都完成後才算完成一組，然後重置方向進度。
        if (completedDirections >= stepCompleted.Length)
        {
            completedSets++;
            UpdateProgressUI();

            for (int i = 0; i < stepCompleted.Length; i++)
            {
                stepCompleted[i] = false;
            }

            completedDirections = 0;

            if (completedSets >= totalSets)
            {
                EndTraining();
                return;
            }
        }

        spawner.SpawnTarget(stepCompleted);
        UpdateStepUI();
    }

    void UpdateStepUI()
    {
        if (stepText != null)
            stepText.text = waitingForCenter ? "方向：回中心" : "方向：" + spawner.currentStepType;
    }

    void UpdateProgressUI()
    {
        if (progressText != null)
            progressText.text = "組數：" + completedSets + "/" + totalSets;
    }

    void EndTraining()
    {
        trainingOver = true;
        PlaySound(finishSound);

        if (finishText != null)
            finishText.SetActive(true);

        if (restartButton != null)
            restartButton.SetActive(true);
    }

    public void RestartTraining()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clip);
    }
}
