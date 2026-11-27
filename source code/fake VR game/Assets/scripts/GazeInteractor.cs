using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class GazeInteractor : MonoBehaviour
{
    [Header("射線偵測設定")]
    [Tooltip("最大可互動距離（公尺）")]
    public float interactDistance = 3.5f;
    [Tooltip("球形射線半徑，增加偽 VR 瞄準細小物品（如桌上的筆）的容錯率")]
    public float gazeRadius = 0.08f;
    [Tooltip("請勾選 Interactable 圖層")]
    public LayerMask interactableLayer;

    [Header("輸入綁定")]
    [Tooltip("請綁定 PlayerControls 中的 Interact Action (對應 Gamepad Button South / A鍵)")]
    public InputActionReference interactAction;

    [Header("準心與動態提示 UI 參考")]
    public Image crosshairImage;
    public Color normalCrosshairColor = new Color(1f, 1f, 1f, 0.75f);
    public Color activeCrosshairColor = new Color(0.3f, 1f, 0.5f, 1f);
    public float normalCrosshairScale = 1f;
    public float activeCrosshairScale = 1.45f;

    [Tooltip("準心旁的互動提示面板（需掛有 CanvasGroup）")]
    public CanvasGroup promptCanvasGroup;
    public TextMeshProUGUI promptActionText;
    [Tooltip("顯示按鍵指引的文字（例如手把顯示 A，鍵盤 Debug 顯示 E）")]
    public TextMeshProUGUI keyIconText;

    [Header("動畫速度")]
    public float uiFadeSpeed = 12f;

    private InteractableObject currentTarget;
    private bool isDebugMode;
    private bool isTouchEnabled;

    void Start()
    {
        isDebugMode = PlayerPrefs.GetInt("DebugMode", 0) == 1;
        isTouchEnabled = PlayerPrefs.GetInt("TouchEnabled", 0) == 1;

        if (promptCanvasGroup != null)
        {
            promptCanvasGroup.alpha = 0f;
            // 若開啟觸控模式，允許玩家直接點擊提示框觸發互動；否則關閉阻擋避免干擾
            promptCanvasGroup.blocksRaycasts = isTouchEnabled;
            promptCanvasGroup.interactable = isTouchEnabled;
        }
    }

    void OnEnable()
    {
        if (interactAction != null)
            interactAction.action.Enable();
    }

    void OnDisable()
    {
        if (interactAction != null)
            interactAction.action.Disable();
    }

    void Update()
    {
        // 遊戲暫停時隱藏提示並停止偵測
        if (Time.timeScale == 0f)
        {
            currentTarget = null;
            UpdatePromptVisuals(false);
            return;
        }

        // 每次更新設定狀態（相容暫停選單即時切換 Debug / 觸控）
        isDebugMode = PlayerPrefs.GetInt("DebugMode", 0) == 1;
        isTouchEnabled = PlayerPrefs.GetInt("TouchEnabled", 0) == 1;

        ScanGazeTarget();
        UpdatePromptVisuals(currentTarget != null);
        HandleInteractInput();
    }

    private void ScanGazeTarget()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        InteractableObject detected = null;

        // 忽略 Trigger 碰撞體，避免未來的任務觸發區擋住視線射線
        if (Physics.SphereCast(ray, gazeRadius, out RaycastHit hit, interactDistance, interactableLayer, QueryTriggerInteraction.Ignore))
        {
            // 支援 Collider 掛在子物件、腳本掛在父物件的結構
            InteractableObject obj = hit.collider.GetComponentInParent<InteractableObject>();
            if (obj != null && obj.isInteractable)
            {
                detected = obj;
            }
        }

        currentTarget = detected;
    }

    private void UpdatePromptVisuals(bool hasTarget)
    {
        // 1. 更新中央準心顏色與縮放回饋
        if (crosshairImage != null)
        {
            Color targetColor = hasTarget ? activeCrosshairColor : normalCrosshairColor;
            crosshairImage.color = Color.Lerp(crosshairImage.color, targetColor, uiFadeSpeed * Time.deltaTime);

            float targetScale = hasTarget ? activeCrosshairScale : normalCrosshairScale;
            Vector3 scaleVec = Vector3.one * targetScale;
            crosshairImage.rectTransform.localScale = Vector3.Lerp(crosshairImage.rectTransform.localScale, scaleVec, uiFadeSpeed * Time.deltaTime);
        }

        // 2. 更新準心旁動態文字與按鍵指引
        if (promptCanvasGroup != null)
        {
            float targetAlpha = hasTarget ? 1f : 0f;
            promptCanvasGroup.alpha = Mathf.MoveTowards(promptCanvasGroup.alpha, targetAlpha, uiFadeSpeed * Time.deltaTime);

            if (hasTarget && currentTarget != null)
            {
                if (promptActionText != null)
                    promptActionText.text = currentTarget.GetCurrentPrompt();

                if (keyIconText != null)
                {
                    // 優先顯示手把 A 鍵；若無手把且開啟 Debug 則提示 E 鍵
                    bool hasGamepad = Gamepad.current != null;
                    if (hasGamepad)
                        keyIconText.text = "A";
                    else if (isDebugMode)
                        keyIconText.text = "E";
                    else
                        keyIconText.text = "A";
                }
            }
        }
    }

    private void HandleInteractInput()
    {
        if (currentTarget == null) return;

        bool triggered = false;

        // 1. 優先讀取 Input Action Asset 綁定的 Interact (Gamepad Button South / A 鍵)
        if (interactAction != null && interactAction.action.WasPressedThisFrame())
        {
            triggered = true;
        }
        // 2. 保底直接讀取手把 A 鍵（避免尚未手動拖入 InputActionReference 時無反應）
        else if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            triggered = true;
        }
        // 3. Debug 模式下支援鍵盤 E 鍵測試
        else if (isDebugMode && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            triggered = true;
        }

        if (triggered)
        {
            TriggerCurrentInteraction();
        }
    }

    /// <summary>
    /// 供按鍵輸入或觸控模式 UI 按鈕點擊呼叫
    /// </summary>
    public void TriggerCurrentInteraction()
    {
        if (currentTarget != null && currentTarget.isInteractable)
        {
            currentTarget.TriggerInteract();
        }
    }
}