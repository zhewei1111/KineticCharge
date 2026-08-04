# KineticCharge

一款以 **Azure Kinect** 驅動的體感復健／訓練小遊戲。玩家透過身體往前、後、左、右與斜向移動，觸碰隨機出現的目標；每次完成目標後都必須回到中心位置，完成全部八個方向即完成一組訓練。

## 功能

- Azure Kinect 深度影像追蹤玩家位置
- 支援八個移動方向：前、後、左、右與四個斜向
- 遊戲開始前 3 秒倒數，提供命中、回到中心與完成提示音
- 自動校正站立中心；按下 `R` 可隨時重新校正
- 可在 Unity Inspector 調整感測範圍、平滑程度、目標距離與訓練組數

## 操作方式

1. 面向 Azure Kinect，於遊戲啟動時站在中心位置等待倒數和校正完成。
2. 倒數結束後，依畫面目標方向移動身體，讓玩家角色碰到目標。
3. 碰到目標後回到中心，再進行下一個方向。
4. 完成八個方向即完成一組訓練。
5. 若中心位置不準確，按 `R` 重新校正。

## 系統需求

- Windows
- Unity `6000.2.12f1`（Unity 6）
- Azure Kinect DK
- 已安裝 Azure Kinect Sensor SDK 與裝置驅動程式

> 專案已包含 Azure Kinect 的 Unity 外掛 DLL；執行時仍需要正確安裝 Azure Kinect Sensor SDK，並連接裝置。

## 開啟專案

1. 使用 Unity Hub 加入此資料夾。
2. 選擇 Unity `6000.2.12f1` 開啟。
3. 開啟 `Assets/Scenes/SampleScene.unity`。
4. 連接 Azure Kinect DK，進入 Play Mode。

## 可調整項目

在場景中的 `KinectManager` 可調整：

- `minDepth`／`maxDepth`：辨識玩家的深度範圍（毫米）
- `maxPhysicalRangeX`：左右活動範圍
- `maxPhysicalRangeForwardZ`／`maxPhysicalRangeBackwardZ`：前後活動範圍
- `smoothFactor`：追蹤平滑程度

在 `PlayerController` 與 `TargetSpawner` 可調整訓練組數、各方向的目標距離及判定範圍。

## 使用技術

- Unity 6 + Universal Render Pipeline（URP）
- C#
- Azure Kinect Sensor SDK
- TextMesh Pro

## 專案結構

```text
Assets/
├── code/
│   ├── KinectManager.cs     # 深度影像讀取、玩家位置與校正
│   ├── PlayerController.cs  # 遊戲流程、移動與 UI
│   └── TargetSpawner.cs     # 八方向目標生成
├── Plugins/                 # Azure Kinect 原生與 .NET 外掛
├── Scenes/SampleScene.unity
└── sound/                   # 遊戲音效
```

## 注意事項

- 請確認感測器前方空間平坦、光線與站位穩定，避免其他人進入感測範圍。
- 若無法啟動感測器，請確認裝置已連接、沒有被其他程式占用，並已安裝 Azure Kinect Sensor SDK。
