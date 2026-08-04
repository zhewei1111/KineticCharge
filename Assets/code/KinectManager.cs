using UnityEngine;
using Microsoft.Azure.Kinect.Sensor;
using System;

public class KinectManager : MonoBehaviour
{
    public static KinectManager instance;

    private Device kinectDevice;

    [Header("Kinect 深度設定")]
    // 深度單位是毫米。maxDepth 需要大於玩家最遠站位，否則超過距離會直接偵測不到。
    public int minDepth = 500;
    public int maxDepth = 5500;

    [Header("玩家偵測設定")]
    // 將畫面中的深度點分群，找出最像玩家身體的那一群，降低地板或背景被誤抓的機率。
    public int minDetectedPixels = 20;
    public int scanStep = 6;
    public int edgeMargin = 30;
    public int depthBinSize = 200;
    [Range(0.05f, 0.95f)]
    public float scanTopPercent = 0.12f;
    [Range(0.05f, 0.95f)]
    public float scanBottomPercent = 0.68f;

    [Header("現實移動範圍（毫米）")]
    // 現實移動到遊戲畫面的映射範圍。數值越小，現實移動一點點，遊戲控制點會移動更多。
    public float maxPhysicalRangeX = 2500f;
    // 前後分開設定，因為玩家通常站在 Kinect 前方，往前可走距離較短，往後可走距離較長。
    public float maxPhysicalRangeForwardZ = 2500f;
    public float maxPhysicalRangeBackwardZ = 2500f;

    [Range(0.01f, 1f)]
    public float smoothFactor = 0.2f;

    [Header("控制設定")]
    public bool invertX = true;
    public KeyCode recalibrateKey = KeyCode.R;

    // 給 PlayerController 使用的標準化座標，範圍固定在 -1 到 1。
    public Vector2 playerTargetPercent;

    [Header("除錯資訊")]
    // Play Mode 時可在 Inspector 觀察，確認 Kinect 是否真的抓到玩家深度。
    public int detectedPixelCount;
    public float detectedDepth;

    private float smoothedWorldX = 0f;
    private float smoothedWorldZ = 0f;
    private bool hasLastFrame = false;

    private float centerWorldX = 0f;
    private float centerWorldZ = 0f;
    private bool isCalibrated = false;
    private float warmUpTimer = 1.5f;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    void Start()
    {
        try
        {
            kinectDevice = Device.Open(0);
            var config = new DeviceConfiguration
            {
                ColorFormat = ImageFormat.ColorBGRA32,
                ColorResolution = ColorResolution.R720p,
                // NFOV_2x2Binned 較適合看比較遠的深度，適合目前往後移動的訓練場景。
                DepthMode = DepthMode.NFOV_2x2Binned,
                SynchronizedImagesOnly = true,
            };

            kinectDevice.StartCameras(config);
            Debug.Log("Kinect started. Stand still for calibration.");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to start Kinect: " + e.Message);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(recalibrateKey))
            Recalibrate();

        if (kinectDevice == null)
            return;

        try
        {
            using (Capture frame = kinectDevice.GetCapture(TimeSpan.FromMilliseconds(20)))
            {
                if (frame != null && frame.Depth != null)
                {
                    ReadOnlySpan<ushort> depthData = frame.Depth.GetPixels<ushort>().Span;
                    DetectPlayerAbsolute(depthData, frame.Depth.WidthPixels, frame.Depth.HeightPixels);
                }
            }
        }
        catch (TimeoutException)
        {
        }
        catch (Exception e)
        {
            Debug.LogWarning("Failed to read Kinect frame: " + e.Message);
        }
    }

    void DetectPlayerAbsolute(ReadOnlySpan<ushort> depthData, int width, int height)
    {
        if (!TryFindPlayerCluster(depthData, width, height, out float rawWorldX, out float rawWorldZ, out int count))
            return;

        detectedPixelCount = count;
        detectedDepth = rawWorldZ;

        if (!hasLastFrame)
        {
            smoothedWorldX = rawWorldX;
            smoothedWorldZ = rawWorldZ;
            hasLastFrame = true;
            return;
        }

        smoothedWorldX = Mathf.Lerp(smoothedWorldX, rawWorldX, smoothFactor);
        smoothedWorldZ = Mathf.Lerp(smoothedWorldZ, rawWorldZ, smoothFactor);

        if (warmUpTimer > 0)
        {
            warmUpTimer -= Time.deltaTime;
            centerWorldX = smoothedWorldX;
            centerWorldZ = smoothedWorldZ;
            isCalibrated = true;
            playerTargetPercent = Vector2.zero;
            return;
        }

        if (!isCalibrated)
            return;

        float deltaX = smoothedWorldX - centerWorldX;
        float deltaZ = smoothedWorldZ - centerWorldZ;

        // 將玩家相對於校正中心的現實位移轉成 -1 到 1，之後由 PlayerController 映射到球場。
        float percentX = deltaX / maxPhysicalRangeX;
        float zRange = deltaZ < 0f ? maxPhysicalRangeForwardZ : maxPhysicalRangeBackwardZ;
        float percentY = -deltaZ / Mathf.Max(1f, zRange);

        if (invertX)
            percentX = -percentX;

        playerTargetPercent = new Vector2(
            Mathf.Clamp(percentX, -1f, 1f),
            Mathf.Clamp(percentY, -1f, 1f)
        );
    }

    bool TryFindPlayerCluster(ReadOnlySpan<ushort> depthData, int width, int height, out float worldX, out float worldZ, out int pixelCount)
    {
        worldX = 0f;
        worldZ = 0f;
        pixelCount = 0;

        // 把深度依距離切成多個區間，再選出最穩定、最靠近畫面中央的玩家區塊。
        int binSize = Mathf.Max(50, depthBinSize);
        int binCount = Mathf.CeilToInt((maxDepth - minDepth + 1) / (float)binSize);
        if (binCount <= 0)
            return false;

        int[] counts = new int[binCount];
        double[] totalX = new double[binCount];
        double[] totalZ = new double[binCount];
        double[] scores = new double[binCount];

        int startY = Mathf.Clamp(Mathf.RoundToInt(height * scanTopPercent), 0, height - 1);
        int endY = Mathf.Clamp(Mathf.RoundToInt(height * scanBottomPercent), startY + 1, height);
        int startX = Mathf.Clamp(edgeMargin, 0, width - 1);
        int endX = Mathf.Clamp(width - edgeMargin, startX + 1, width);
        double centerX = width / 2.0;

        // 只掃描身體比較常出現的畫面區域，盡量避開太下面的地板。
        for (int y = startY; y < endY; y += scanStep)
        {
            for (int x = startX; x < endX; x += scanStep)
            {
                int depth = depthData[y * width + x];
                if (depth < minDepth || depth > maxDepth)
                    continue;

                int bin = Mathf.Clamp((depth - minDepth) / binSize, 0, binCount - 1);
                double normalizedDistanceFromCenter = Math.Abs(x - centerX) / centerX;
                double centerWeight = 1.0 - Math.Min(normalizedDistanceFromCenter, 1.0) * 0.45;
                double worldXSample = (x - centerX) * (depth / (width * 0.65));

                counts[bin]++;
                totalX[bin] += worldXSample;
                totalZ[bin] += depth;
                scores[bin] += centerWeight;
            }
        }

        int bestBin = -1;
        double bestScore = 0;
        float referenceDepth = hasLastFrame ? smoothedWorldZ : centerWorldZ;

        // 優先選擇像玩家身體的深度群，並保留一點上一幀的連續性，避免突然跳到背景。
        for (int i = 0; i < binCount; i++)
        {
            if (counts[i] < minDetectedPixels)
                continue;

            double averageDepth = totalZ[i] / counts[i];
            double continuityWeight = hasLastFrame
                ? 1.0 / (1.0 + Math.Abs(averageDepth - referenceDepth) / 1200.0)
                : 1.0;
            double score = scores[i] * continuityWeight;

            if (score > bestScore)
            {
                bestScore = score;
                bestBin = i;
            }
        }

        if (bestBin < 0)
            return false;

        pixelCount = counts[bestBin];
        worldX = (float)(totalX[bestBin] / pixelCount);
        worldZ = (float)(totalZ[bestBin] / pixelCount);
        return true;
    }

    void Recalibrate()
    {
        warmUpTimer = 1.5f;
        hasLastFrame = false;
        isCalibrated = false;
        playerTargetPercent = Vector2.zero;
        Debug.Log("Kinect recalibration started.");
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;

        if (kinectDevice == null)
            return;

        kinectDevice.StopCameras();
        kinectDevice.Dispose();
        kinectDevice = null;
    }
}
