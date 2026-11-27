using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using TMPro;

[System.Serializable]
public class TaskStepData
{
    [TextArea(1, 3)]
    [Tooltip("顯示在左中側面板的步驟描述")]
    public string stepDescription;

    [Header("連動場景互動物件（選填）")]
    [Tooltip("進入此步驟時，自動啟用並更新提示文字的目標物件")]
    public InteractableObject targetInteractable;
    [Tooltip("進入此步驟時，覆寫該物件在準心旁顯示的互動文字（留空則用物件預設值）")]
    public string overrideInteractPrompt;

    [Header("隔牆空間導引設定（預留給下一步導引物系統）")]
    public Transform waypointTarget;
    [Tooltip("勾選後，此步驟會顯示可隔牆看到的導引標記；若要考驗純視覺辨識（如色盲找筆）請取消勾選")]
    public bool showThroughWallGuide = false;

    [Header("完成此步驟時觸發的事件")]
    public UnityEvent onStepCompleted;
}

[System.Serializable]
public class DefectTaskProfile
{
    [Tooltip("對應 SelectedDefect 編號 (0:正常, 1:色盲, 2:近視, 3:偏頭痛, 4:K他命, 5:遠視, 6:蘑菇, 7:ReversePhi, 8:黃斑部)")]
    public int defectId;
    public string modeDisplayName;
    public string taskTitle;
    [TextArea(2, 4)]
    public string taskHint;
    public List<TaskStepData> steps = new List<TaskStepData>();
}

public class TaskManager : MonoBehaviour
{
    public static TaskManager Instance { get; private set; }

    [Header("左中側任務指引 UI 參考")]
    public CanvasGroup panelCanvasGroup;
    public TextMeshProUGUI modeTagText;
    public TextMeshProUGUI taskTitleText;
    public TextMeshProUGUI taskStepsText;
    public TextMeshProUGUI taskHintText;

    [Header("步驟狀態顏色設定")]
    public Color activeStepColor = new Color(1f, 0.84f, 0.35f);    // 進行中：暖金黃
    public Color pendingStepColor = new Color(0.85f, 0.88f, 0.92f); // 未完成：柔和白
    public Color completedStepColor = new Color(0.55f, 0.6f, 0.65f); // 已完成：灰階劃線
    public Color allClearColor = new Color(0.35f, 0.95f, 0.55f);    // 全部完成：亮綠

    [Header("9 種視知覺專屬任務配置（可在右上角選單一鍵填入預設範本）")]
    public List<DefectTaskProfile> taskProfiles = new List<DefectTaskProfile>();

    [Header("全域事件")]
    public UnityEvent onAllStepsCompleted;

    private DefectTaskProfile activeProfile;
    private int currentStepIndex = 0;
    private bool isTaskCompleted = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        int selectedDefect = PlayerPrefs.GetInt("SelectedDefect", 0);
        LoadTaskForDefect(selectedDefect);
    }

    void Update()
    {
        // Debug 模式下按 N 鍵可快速推進一個任務步驟方便測試
        if (PlayerPrefs.GetInt("DebugMode", 0) == 1 && Keyboard.current != null)
        {
            if (Keyboard.current.nKey.wasPressedThisFrame)
            {
                CompleteCurrentStep();
            }
        }
    }

    /// <summary>
    /// 依據障礙編號載入對應的生活化任務
    /// </summary>
    public void LoadTaskForDefect(int defectId)
    {
        activeProfile = null;
        for (int i = 0; i < taskProfiles.Count; i++)
        {
            if (taskProfiles[i].defectId == defectId)
            {
                activeProfile = taskProfiles[i];
                break;
            }
        }

        if (activeProfile == null)
        {
            Debug.LogWarning($"[TaskManager] 找不到 defectId = {defectId} 的任務配置，已隱藏任務面板。");
            if (panelCanvasGroup != null) panelCanvasGroup.alpha = 0f;
            return;
        }

        if (panelCanvasGroup != null) panelCanvasGroup.alpha = 1f;
        currentStepIndex = 0;
        isTaskCompleted = false;

        ActivateStep(0);
        RefreshTaskUI();
    }

    private void ActivateStep(int index)
    {
        if (activeProfile == null || index < 0 || index >= activeProfile.steps.Count) return;

        TaskStepData step = activeProfile.steps[index];
        if (step.targetInteractable != null)
        {
            step.targetInteractable.SetInteractable(true);
            if (!string.IsNullOrEmpty(step.overrideInteractPrompt))
            {
                step.targetInteractable.SetRuntimePrompt(step.overrideInteractPrompt);
            }
        }
    }

    /// <summary>
    /// 完成當前正在進行的步驟（可直接綁定到 InteractableObject 的 OnInteract 事件）
    /// </summary>
    public void CompleteCurrentStep()
    {
        if (activeProfile == null || isTaskCompleted) return;

        if (currentStepIndex >= 0 && currentStepIndex < activeProfile.steps.Count)
        {
            TaskStepData finishedStep = activeProfile.steps[currentStepIndex];
            finishedStep.onStepCompleted?.Invoke();
        }

        currentStepIndex++;

        if (currentStepIndex >= activeProfile.steps.Count)
        {
            isTaskCompleted = true;
            Debug.Log($"[TaskManager] 任務全數完成：{activeProfile.taskTitle}");
            onAllStepsCompleted?.Invoke();
        }
        else
        {
            ActivateStep(currentStepIndex);
        }

        RefreshTaskUI();
    }

    /// <summary>
    /// 指定完成特定編號的步驟（防呆：只有當玩家正好處於該步驟時才會推進）
    /// </summary>
    public void CompleteSpecificStep(int stepIndex)
    {
        if (stepIndex == currentStepIndex)
        {
            CompleteCurrentStep();
        }
    }

    /// <summary>
    /// 取得當前步驟設定（供未來的隔牆空間導引系統讀取）
    /// </summary>
    public TaskStepData GetCurrentStepData()
    {
        if (activeProfile == null || isTaskCompleted) return null;
        if (currentStepIndex < 0 || currentStepIndex >= activeProfile.steps.Count) return null;
        return activeProfile.steps[currentStepIndex];
    }

    public void RefreshTaskUI()
    {
        if (activeProfile == null) return;

        if (modeTagText != null)
            modeTagText.text = $"【體驗模式：{activeProfile.modeDisplayName}】";

        if (taskTitleText != null)
        {
            if (isTaskCompleted)
            {
                string clearHex = ColorUtility.ToHtmlStringRGB(allClearColor);
                taskTitleText.text = $"<color=#{clearHex}>✔ {activeProfile.taskTitle}（已完成）</color>";
            }
            else
            {
                taskTitleText.text = activeProfile.taskTitle;
            }
        }

        if (taskStepsText != null)
        {
            string activeHex = ColorUtility.ToHtmlStringRGB(activeStepColor);
            string pendingHex = ColorUtility.ToHtmlStringRGB(pendingStepColor);
            string doneHex = ColorUtility.ToHtmlStringRGB(completedStepColor);

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < activeProfile.steps.Count; i++)
            {
                string desc = activeProfile.steps[i].stepDescription;
                if (i < currentStepIndex)
                {
                    sb.AppendLine($"<color=#{doneHex}>☑ <s>{desc}</s></color>");
                }
                else if (i == currentStepIndex && !isTaskCompleted)
                {
                    sb.AppendLine($"<color=#{activeHex}>▶ <b>{desc}</b></color>");
                }
                else
                {
                    sb.AppendLine($"<color=#{pendingHex}>☐ {desc}</color>");
                }
            }
            taskStepsText.text = sb.ToString().TrimEnd();
        }

        if (taskHintText != null)
        {
            if (isTaskCompleted)
            {
                taskHintText.text = "你已完成此視知覺體驗目標！可繼續在館內自由觀察，或按 Start 鍵切換其他體驗。";
            }
            else
            {
                taskHintText.text = string.IsNullOrWhiteSpace(activeProfile.taskHint)
                    ? string.Empty
                    : $"💡 {activeProfile.taskHint}";
            }
        }
    }

    /// <summary>
    /// 在 Inspector 點擊右上角齒輪/三點選單執行，即可一鍵生成 9 種視知覺生活化任務範本
    /// </summary>
    [ContextMenu("一鍵填入 9 種視知覺生活化預設任務")]
    public void PopulateDefaultLifestyleTasks()
    {
        taskProfiles = new List<DefectTaskProfile>
        {
            new DefectTaskProfile
            {
                defectId = 0,
                modeDisplayName = "正常視覺 (基準對照)",
                taskTitle = "展館報到與空間導覽",
                taskHint = "留意大廳遠處文字與桌上物件的清晰度，作為後續體驗障礙的基準對照。",
                steps = new List<TaskStepData>
                {
                    new TaskStepData { stepDescription = "前往大廳櫃檯與接待人員／簽到簿互動報到", showThroughWallGuide = true },
                    new TaskStepData { stepDescription = "前往閱讀桌檢視展館導覽手冊與色卡基準", showThroughWallGuide = true }
                }
            },
            new DefectTaskProfile
            {
                defectId = 1,
                modeDisplayName = "紅綠色盲",
                taskTitle = "尋找你的指定座位",
                taskHint = "紅綠色盲會讓純紅與純綠褪為相近的土黃灰階，請仔細比對桌上文具的明暗差異。",
                steps = new List<TaskStepData>
                {
                    new TaskStepData { stepDescription = "前往大廳櫃檯查看座位表（提示：你的桌上放著『紅色原子筆』與『綠色書本』）", showThroughWallGuide = true },
                    new TaskStepData { stepDescription = "在閱讀區多張書桌中，找出放有紅色原子筆的座位並拿取確認", showThroughWallGuide = false }
                }
            },
            new DefectTaskProfile
            {
                defectId = 2,
                modeDisplayName = "近視",
                taskTitle = "確認遠處看板的開放時段",
                taskHint = "遠處光線無法聚焦於視網膜上，試著走近展示牆縮短觀察距離。",
                steps = new List<TaskStepData>
                {
                    new TaskStepData { stepDescription = "站在大廳中央嘗試辨識遠處牆上發光螢幕的展廳時刻表", showThroughWallGuide = true },
                    new TaskStepData { stepDescription = "走近發光螢幕直到看清文字，並與螢幕旁的報到機互動", showThroughWallGuide = true }
                }
            },
            new DefectTaskProfile
            {
                defectId = 3,
                modeDisplayName = "偏頭痛預兆",
                taskTitle = "閃爍盲點下的尋路與休息",
                taskHint = "視野中央或側邊出現閃爍暗點時，可轉動頭部利用周邊餘光確認動線。",
                steps = new List<TaskStepData>
                {
                    new TaskStepData { stepDescription = "在鋸齒閃光與盲點干擾下，前往大廳櫃檯領取舒緩眼罩", showThroughWallGuide = true },
                    new TaskStepData { stepDescription = "前往光線柔和的第三展示間休憩區坐下休息", showThroughWallGuide = true }
                }
            },
            new DefectTaskProfile
            {
                defectId = 4,
                modeDisplayName = "K他命解離",
                taskTitle = "空間尺度失調下的取水挑戰",
                taskHint = "身體大小感與距離透視正在劇烈縮放，請透過桌椅比例重新判斷伸手距離。",
                steps = new List<TaskStepData>
                {
                    new TaskStepData { stepDescription = "克服無重力漂浮與房間縮放感，走近擺放日常傢俱的休憩桌", showThroughWallGuide = true },
                    new TaskStepData { stepDescription = "將準心對準桌上的馬克杯並按下互動鍵拿取飲水", showThroughWallGuide = false }
                }
            },
            new DefectTaskProfile
            {
                defectId = 5,
                modeDisplayName = "遠視",
                taskTitle = "閱讀近距離展品說明卡",
                taskHint = "距離過近時水晶體調節力不足會使文字糊成一團，試著往後退幾步拉開距離。",
                steps = new List<TaskStepData>
                {
                    new TaskStepData { stepDescription = "前往櫃檯或展示桌拿取近距離展品說明卡", showThroughWallGuide = true },
                    new TaskStepData { stepDescription = "退後至約 1.5 公尺處看清卡片內容，並按下桌邊確認鈕", showThroughWallGuide = false }
                }
            },
            new DefectTaskProfile
            {
                defectId = 6,
                modeDisplayName = "致幻蘑菇",
                taskTitle = "色彩流動中的形狀辨識",
                taskHint = "物體表面色彩正隨時間循環偏移且邊界消融，請改以『幾何輪廓』辨識目標。",
                steps = new List<TaskStepData>
                {
                    new TaskStepData { stepDescription = "進入色彩豐富的展示間，觀察光源與牆面的色相流動", showThroughWallGuide = true },
                    new TaskStepData { stepDescription = "不依賴表面顏色，找出展台上的『正立方體幾何雕塑』並互動", showThroughWallGuide = false }
                }
            },
            new DefectTaskProfile
            {
                defectId = 7,
                modeDisplayName = "Reverse Phi 錯覺",
                taskTitle = "逆向殘影干擾下的儀器校準",
                taskHint = "轉頭時高對比條紋會產生反方向蠕動的負片殘影，請放慢轉頭速度穩定準心。",
                steps = new List<TaskStepData>
                {
                    new TaskStepData { stepDescription = "走近設有高對比條紋與幾何圖案的視覺測試牆", showThroughWallGuide = true },
                    new TaskStepData { stepDescription = "克服轉視角時的反向運動錯覺，對準牆上的校準開關並啟動", showThroughWallGuide = false }
                }
            },
            new DefectTaskProfile
            {
                defectId = 8,
                modeDisplayName = "黃斑部病變",
                taskTitle = "利用『偏心注視』辨識中心盲區",
                taskHint = "直視正中央會被灰暗盲點與扭曲遮蔽，請微微偏轉視角，用餘光看清目標。",
                steps = new List<TaskStepData>
                {
                    new TaskStepData { stepDescription = "前往掛有視力表與導覽海報的展間牆面", showThroughWallGuide = true },
                    new TaskStepData { stepDescription = "利用餘光避開中心暗點，辨識海報旁的正確指示開關並互動", showThroughWallGuide = false }
                }
            }
        };

        Debug.Log("[TaskManager] 已成功填入 9 種視知覺生活化任務範本！");
    }
}