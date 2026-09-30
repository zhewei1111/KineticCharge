using UnityEngine;
using TMPro;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("限時挑戰設定")]
    [Tooltip("開始倒數結束後的遊戲時間")]
    public float gameDurationSeconds = 60f;
    public int basicGoal = 12;
    public int challengeGoal = 15;
    public int excellentGoal = 18;

    [Header("單一目標時間限制")]
    [Tooltip("從目標出現到回到中心的最長秒數；超時會直接換下一個目標且不計分")]
    [Min(1f)]
    public float targetTimeLimitSeconds = 6f;

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
    [Tooltip("碰到目標後，延遲多久才允許判定回中心，避免 Kinect 瞬間抖動直接計分")]
    [Min(0f)]
    public float centerReturnDelaySeconds = 0.5f;

    // 畫面文字與結束按鈕的場景參考。
    public TMP_Text stepText;
    public TMP_Text progressText;
    public TMP_Text countText;
    public TMP_Text outOfRangeText;
    public GameObject finishText;
    public GameObject restartButton;
    public GameObject againButton;

    [Header("起始畫面設定")]
    public GameObject centerZone;
    public GameObject tip;
    public GameObject startButton;
    public GameObject titleText;
    public GameObject rulesText;

    [Header("開始倒數設定")]
    public float startCountdownSeconds = 5f;

    [Header("音效設定")]
    public AudioClip targetHitSound;
    public AudioClip centerReturnSound;
    public AudioClip countdownTickSound;
    [Tooltip("進入最後 10 秒時播放一次的提示音，建議長度約 1 秒")]
    public AudioClip lastTenSecondsSound;
    [Tooltip("最後 9 到 1 秒使用的倒數音效；未指定時會使用一般倒數音效")]
    public AudioClip finalCountdownSound;
    public AudioClip startSound;
    public AudioClip finishSound;

    // 限時挑戰的進度狀態。
    private int completedReturns = 0;
    private float remainingGameTime;
    private float remainingTargetTime;
    private float centerReturnDelayRemaining;
    private bool isCenterReturnArmed;
    private bool trainingOver = false;
    private bool gameStarted = false;
    private bool isInitialized = false;
    private bool waitingForCenter = false;
    private bool showingOutOfRangeWarning = false;
    private int lastFinalCountdownSecond = -1;
    private Coroutine countdownCoroutine;
    // 場景中的目標生成器與播放音效的元件。
    private TargetSpawner spawner;
    private AudioSource audioSource;

    // 限時模式允許八個方向持續隨機出現，因此所有值維持 false。
    private readonly bool[] availableDirections = new bool[8];

    void Awake()
    {
        // 建立 UI、音效與目標生成器的必要參考；實際遊戲由開始按鈕觸發。
        ResolveCountText();
        ResolveOutOfRangeText();

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

        remainingGameTime = gameDurationSeconds;
        UpdateProgressUI();

        if (finishText != null)
            finishText.SetActive(false);

        if (restartButton != null)
            restartButton.SetActive(false);

        if (againButton != null)
            againButton.SetActive(false);

        isInitialized = true;
    }

    // 由 StartButton 呼叫：切換起始畫面為遊戲畫面，倒數最後一秒校正本局中心。
    public void BeginTraining()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (!isInitialized || gameStarted)
            return;

        if (startButton != null)
            startButton.SetActive(false);

        if (titleText != null)
            titleText.SetActive(false);

        if (rulesText != null)
            rulesText.SetActive(false);

        if (centerZone != null)
            centerZone.SetActive(true);

        if (tip != null)
            tip.SetActive(true);

        if (stepText != null)
            stepText.gameObject.SetActive(true);

        if (progressText != null)
            progressText.gameObject.SetActive(true);

        if (countText != null)
            countText.gameObject.SetActive(true);

        if (againButton != null)
            againButton.SetActive(true);

        StartRoundCountdown();
    }

    void Update()
    {
        // 遊戲尚未開始或已結束時，不再處理移動與計分。
        if (trainingOver || !gameStarted)
            return;

        // Kinect 暫時失去玩家時凍結遊戲，避免輸入回到零而誤判回中心。
        if (KinectManager.instance == null || !KinectManager.instance.HasValidPlayerDetection)
        {
            ShowOutOfRangeWarning();
            return;
        }

        HideOutOfRangeWarning();

        remainingGameTime -= Time.deltaTime;
        if (remainingGameTime <= 0f)
        {
            remainingGameTime = 0f;
            UpdateProgressUI();
            EndTraining();
            return;
        }

        UpdateProgressUI();
        UpdateFinalCountdown();

        // 目標出現到回中心都必須在時限內完成；Kinect 暫時失去偵測時會在上方提前 return，限時也會暫停。
        if (spawner.CurrentTarget != null || waitingForCenter)
        {
            remainingTargetTime -= Time.deltaTime;
            if (remainingTargetTime <= 0f)
            {
                SkipCurrentTarget();
                return;
            }
        }

        Vector2 playerInput = KinectManager.instance.playerTargetPercent;
        FollowKinectOnTargetLine(playerInput);

        // 碰到目標後必須先回中心，才算完成一次完整往返。
        if (waitingForCenter)
        {
            // 必須先持續留在中心外一小段時間，才接受回中心，避免碰到目標瞬間的 Kinect 抖動被誤算。
            if (!isCenterReturnArmed)
            {
                if (Vector2.Distance(transform.position, spawner.CenterPosition) > reachDistance * 2f)
                    centerReturnDelayRemaining -= Time.deltaTime;
                else
                    centerReturnDelayRemaining = centerReturnDelaySeconds;

                if (centerReturnDelayRemaining <= 0f)
                    isCenterReturnArmed = true;

                return;
            }

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
        // 每局前四秒可走回中心貼紙，最後一秒依目前玩家站位校正中心。
        float remainingTime = startCountdownSeconds;
        int lastShownSecond = -1;
        bool hasStartedFinalCalibration = false;

        while (remainingTime > 0f)
        {
            int shownSecond = Mathf.CeilToInt(remainingTime);
            if (countText != null)
                countText.text = shownSecond.ToString();

            if (shownSecond != lastShownSecond)
            {
                if (shownSecond == 1 && !hasStartedFinalCalibration)
                {
                    KinectManager.instance?.Recalibrate();
                    hasStartedFinalCalibration = true;
                }

                PlaySound(countdownTickSound);
                lastShownSecond = shownSecond;
            }

            remainingTime -= Time.deltaTime;
            yield return null;
        }

        if (countText != null)
            countText.gameObject.SetActive(false);

        if (tip != null)
            tip.SetActive(false);

        PlaySound(startSound);

        yield return new WaitForSeconds(0.3f);

        gameStarted = true;
        SpawnNextTarget();
        UpdateStepUI();
    }

    void ResolveCountText()
    {
        // Inspector 沒有手動指定時，依物件名稱尋找倒數文字。
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

    void ShowOutOfRangeWarning()
    {
        // 顯示獨立警告文字；倒數文字 CountText 不會被覆寫。
        if (showingOutOfRangeWarning || outOfRangeText == null)
            return;

        showingOutOfRangeWarning = true;
        outOfRangeText.color = Color.red;
        outOfRangeText.fontStyle = FontStyles.Bold;
        outOfRangeText.text = "已超出偵測範圍\n請回到可偵測區域";
        outOfRangeText.gameObject.SetActive(true);
    }

    void HideOutOfRangeWarning()
    {
        // Kinect 重新抓到玩家後，隱藏警告並恢復限時挑戰。
        if (!showingOutOfRangeWarning || outOfRangeText == null)
            return;

        showingOutOfRangeWarning = false;
        outOfRangeText.gameObject.SetActive(false);
    }

    void ResolveOutOfRangeText()
    {
        if (outOfRangeText != null)
            return;

        // 尋找使用者在 Canvas 中建立的獨立警告文字框，包含一開始關閉的物件。
        TMP_Text[] textComponents = FindObjectsByType<TMP_Text>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (TMP_Text textComponent in textComponents)
        {
            if (textComponent.gameObject.name == "OutOfRangeText")
            {
                outOfRangeText = textComponent;
                outOfRangeText.gameObject.SetActive(false);
                return;
            }
        }

        Debug.LogWarning("PlayerController could not find OutOfRangeText.");
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 目標與中心統一由 Update 的距離判定結算，避免碰撞事件重複觸發。
    }

    void FollowKinectOnTargetLine(Vector2 playerInput)
    {
        // 將 Kinect 的相對位移投影到「中心至目前目標」的固定直線上。
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
        // 控制點碰到目標後，切換為等待回中心的狀態。
        if (waitingForCenter || trainingOver)
            return;

        if (targetObject != null)
        {
            transform.position = targetObject.transform.position;
            Destroy(targetObject);
        }

        PlaySound(targetHitSound);

        waitingForCenter = true;
        centerReturnDelayRemaining = centerReturnDelaySeconds;
        isCenterReturnArmed = false;
        UpdateStepUI();
    }

    void CompleteCenterReturn()
    {
        // 只有從目標回到中心才增加一次往返分數。
        if (!waitingForCenter || trainingOver)
            return;

        waitingForCenter = false;
        centerReturnDelayRemaining = 0f;
        isCenterReturnArmed = false;
        transform.position = spawner.CenterPosition;
        PlaySound(centerReturnSound);

        // 完成一次「碰到目標後回到中心」才算一個往返。
        completedReturns++;
        UpdateProgressUI();

        SpawnNextTarget();
        UpdateStepUI();
    }

    void SpawnNextTarget()
    {
        // 每個新目標都重新計時，時間包含碰到目標與回中心的完整往返。
        waitingForCenter = false;
        centerReturnDelayRemaining = 0f;
        isCenterReturnArmed = false;
        remainingTargetTime = targetTimeLimitSeconds;
        spawner.SpawnTarget(availableDirections);
    }

    void SkipCurrentTarget()
    {
        // 超時不加分，直接移除舊目標並產生下一個隨機方向。
        if (spawner.CurrentTarget != null)
            Destroy(spawner.CurrentTarget);

        SpawnNextTarget();
    }

    void UpdateStepUI()
    {
        // 方向提示已移除，上方只保留計分資訊。
    }

    void UpdateProgressUI()
    {
        // 左側顯示倒計時，上方顯示目前完成的往返次數。
        if (progressText != null)
            progressText.text = "倒計時：" + Mathf.CeilToInt(remainingGameTime) + " 秒";

        if (stepText != null)
            stepText.text = "計分：" + completedReturns + " 下";
    }

    void UpdateFinalCountdown()
    {
        // 最後十秒只保留音效提示，避免中央倒數文字遮住遊戲畫面。
        int shownSecond = Mathf.CeilToInt(remainingGameTime);
        if (shownSecond > 10)
            return;

        if (shownSecond == lastFinalCountdownSecond)
            return;

        lastFinalCountdownSecond = shownSecond;
        if (shownSecond == 10)
            PlaySound(lastTenSecondsSound);
        else
            PlaySound(finalCountdownSound != null ? finalCountdownSound : countdownTickSound);
    }

    void EndTraining()
    {
        // 時間結束後停止輸入，依分數顯示評語與重新開始按鈕。
        trainingOver = true;

        if (countText != null)
            countText.gameObject.SetActive(false);

        PlaySound(finishSound);

        if (finishText != null)
        {
            finishText.SetActive(true);

            TMP_Text finishMessage = finishText.GetComponent<TMP_Text>();
            if (finishMessage != null)
                finishMessage.text = GetFinishMessage();
        }

        if (restartButton != null)
            restartButton.SetActive(true);

        if (againButton != null)
            againButton.SetActive(false);
    }

    string GetFinishMessage()
    {
        // 依事先設定的三個分數門檻選擇結束評語。
        if (completedReturns >= excellentGoal)
            return "完成 " + completedReturns + " 下\n表現優秀，步伐很敏捷！";

        if (completedReturns >= challengeGoal)
            return "完成 " + completedReturns + " 下\n挑戰達成，做得很棒！";

        if (completedReturns >= basicGoal)
            return "完成 " + completedReturns + " 下\n達成目標，做得很好！";

        if (completedReturns >= 8)
            return "完成 " + completedReturns + " 下\n很不錯，繼續保持！";

        return "完成 " + completedReturns + " 下\n慢慢來，保持穩定節奏！";
    }

    public void RestartTraining()
    {
        // 結束後按下再玩一次：直接重設本局進度，不回到起始畫面。
        if (!trainingOver || spawner == null)
            return;

        ResetTrainingRound();
    }

    // 遊戲進行中由 AgainButton 呼叫，立即從五秒倒數開始新的一局。
    public void RestartCurrentTraining()
    {
        if (trainingOver || spawner == null)
            return;

        ResetTrainingRound();
    }

    void ResetTrainingRound()
    {
        // 清除舊目標並重設本局分數、計時與回中心判定。
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }

        if (spawner.CurrentTarget != null)
            Destroy(spawner.CurrentTarget);

        completedReturns = 0;
        remainingGameTime = gameDurationSeconds;
        trainingOver = false;
        gameStarted = false;
        waitingForCenter = false;
        centerReturnDelayRemaining = 0f;
        isCenterReturnArmed = false;
        lastFinalCountdownSecond = -1;
        remainingTargetTime = 0f;
        transform.position = spawner.CenterPosition;
        HideOutOfRangeWarning();
        UpdateProgressUI();

        if (finishText != null)
            finishText.SetActive(false);

        if (restartButton != null)
            restartButton.SetActive(false);

        if (againButton != null)
            againButton.SetActive(true);

        if (tip != null)
            tip.SetActive(true);

        if (countText != null)
            countText.gameObject.SetActive(true);

        StartRoundCountdown();
    }

    void StartRoundCountdown()
    {
        // 確保任何時候只會有一個倒數流程執行。
        if (countdownCoroutine != null)
            StopCoroutine(countdownCoroutine);

        countdownCoroutine = StartCoroutine(StartCountdown());
    }

    void PlaySound(AudioClip clip)
    {
        // 音效欄位尚未指定時直接略過，避免播放時出現錯誤。
        if (clip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clip);
    }
}
