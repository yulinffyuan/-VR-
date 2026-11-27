using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class TaskWaypointGuide : MonoBehaviour
{
    public static TaskWaypointGuide Instance { get; private set; }

    [Header("全域總開關（可隨時在程式或 Inspector 切換）")]
    [Tooltip("關閉後，無論當前任務步驟是否有設定導引目標，都會強制隱藏導引物")]
    public bool isGuideMasterEnabled = true;

    [Header("相機與 UI 參考")]
    [Tooltip("請拖入 Main Camera")]
    public Camera mainCamera;
    [Tooltip("請拖入 Canvas 下的 WaypointGuide_Root")]
    public RectTransform markerRect;
    public CanvasGroup markerCanvasGroup;
    [Tooltip("方向指示箭頭（當目標在畫面外時會自動旋轉指向目標方向）")]
    public RectTransform directionArrow;
    public TextMeshProUGUI distanceText;

    [Header("顯示與邊界參數")]
    [Tooltip("螢幕邊緣安全留白（像素），避免標記貼到 VR 鏡片最邊緣")]
    public float screenEdgeMargin = 75f;
    [Tooltip("當玩家距離目標小於此公尺數時自動淡出，避免擋住準心互動")]
    public float hideNearDistance = 1.3f;
    public float fadeSpeed = 10f;

    [Header("手動覆寫目標（選填，優先權高於 TaskManager）")]
    public Transform manualOverrideTarget;

    private RectTransform canvasRect;
    private float targetAlpha = 0f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (markerRect != null)
        {
            Canvas rootCanvas = markerRect.GetComponentInParent<Canvas>();
            if (rootCanvas != null)
                canvasRect = rootCanvas.GetComponent<RectTransform>();
        }

        if (markerCanvasGroup != null)
        {
            markerCanvasGroup.alpha = 0f;
            markerCanvasGroup.blocksRaycasts = false;
            markerCanvasGroup.interactable = false;
        }
    }

    void Update()
    {
        // Debug 模式下按 G 鍵可隨時開關隔牆導引物
        if (PlayerPrefs.GetInt("DebugMode", 0) == 1 && Keyboard.current != null)
        {
            if (Keyboard.current.gKey.wasPressedThisFrame)
            {
                ToggleGuide();
            }
        }

        UpdateWaypointProjection();
    }

    private void UpdateWaypointProjection()
    {
        if (mainCamera == null || markerRect == null || canvasRect == null) return;

        Transform activeTarget = ResolveCurrentTarget();

        if (!isGuideMasterEnabled || activeTarget == null || Time.timeScale == 0f)
        {
            targetAlpha = 0f;
            ApplySmoothAlpha();
            return;
        }

        // 1. 計算玩家與目標的實際距離
        float distance = Vector3.Distance(mainCamera.transform.position, activeTarget.position);
        if (distanceText != null)
        {
            distanceText.text = $"{distance:F1} m";
        }

        // 靠近目標時自動淡出，讓玩家看清物體並對準準心
        targetAlpha = (distance <= hideNearDistance) ? 0f : 1f;
        ApplySmoothAlpha();

        // 2. 將 3D 世界座標轉換為螢幕座標
        Vector3 screenPos = mainCamera.WorldToScreenPoint(activeTarget.position);
        bool isBehindCamera = screenPos.z < 0f;

        // 若目標在攝影機背後，反轉螢幕座標使其推向畫面邊緣
        if (isBehindCamera)
        {
            screenPos.x = Screen.width - screenPos.x;
            screenPos.y = Screen.height - screenPos.y;
        }

        // 3. 限制標記在螢幕安全邊界內（畫面外吸附邊緣指引）
        float minX = screenEdgeMargin;
        float maxX = Screen.width - screenEdgeMargin;
        float minY = screenEdgeMargin;
        float maxY = Screen.height - screenEdgeMargin;

        bool isOffScreen = isBehindCamera
                           || screenPos.x < minX || screenPos.x > maxX
                           || screenPos.y < minY || screenPos.y > maxY;

        Vector3 clampedScreenPos = screenPos;
        if (isOffScreen)
        {
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dirFromCenter = (new Vector2(screenPos.x, screenPos.y) - screenCenter).normalized;
            if (dirFromCenter.sqrMagnitude < 0.001f) dirFromCenter = Vector2.down;

            float halfW = (maxX - minX) * 0.5f;
            float halfH = (maxY - minY) * 0.5f;

            // 沿方向向量投射到螢幕安全矩形邊框上
            float scaleX = Mathf.Abs(dirFromCenter.x) > 0.001f ? halfW / Mathf.Abs(dirFromCenter.x) : float.MaxValue;
            float scaleY = Mathf.Abs(dirFromCenter.y) > 0.001f ? halfH / Mathf.Abs(dirFromCenter.y) : float.MaxValue;
            float scale = Mathf.Min(scaleX, scaleY);

            Vector2 edgePos = screenCenter + dirFromCenter * scale;
            clampedScreenPos.x = edgePos.x;
            clampedScreenPos.y = edgePos.y;

            // 旋轉箭頭指向目標所在方位
            if (directionArrow != null)
            {
                float angle = Mathf.Atan2(dirFromCenter.y, dirFromCenter.x) * Mathf.Rad2Deg;
                directionArrow.localRotation = Quaternion.Euler(0f, 0f, angle + 90f);
            }
        }
        else
        {
            // 目標已在視野內時，箭頭朝正下方指著目標物件
            if (directionArrow != null)
            {
                directionArrow.localRotation = Quaternion.identity;
            }
        }

        // 4. 換算為 Canvas 局部座標並套用
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, clampedScreenPos, null, out Vector2 localPoint))
        {
            markerRect.anchoredPosition = localPoint;
        }
    }

    private Transform ResolveCurrentTarget()
    {
        if (manualOverrideTarget != null)
            return manualOverrideTarget;

        if (TaskManager.Instance != null)
        {
            TaskStepData step = TaskManager.Instance.GetCurrentStepData();
            if (step != null && step.showThroughWallGuide && step.waypointTarget != null)
            {
                return step.waypointTarget;
            }
        }

        return null;
    }

    private void ApplySmoothAlpha()
    {
        if (markerCanvasGroup == null) return;
        markerCanvasGroup.alpha = Mathf.MoveTowards(markerCanvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
    }

    // --- 外部隨時開關與動態指定接口 ---

    public void SetGuideEnabled(bool enabled)
    {
        isGuideMasterEnabled = enabled;
    }

    public void ToggleGuide()
    {
        isGuideMasterEnabled = !isGuideMasterEnabled;
        Debug.Log($"[TaskWaypointGuide] 隔牆空間導引已{(isGuideMasterEnabled ? "開啟" : "關閉")}");
    }

    public void SetManualTarget(Transform newTarget)
    {
        manualOverrideTarget = newTarget;
    }

    public void ClearManualTarget()
    {
        manualOverrideTarget = null;
    }
}