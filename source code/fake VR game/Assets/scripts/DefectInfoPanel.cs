using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

[System.Serializable]
public class DefectInfoData
{
    public string title;
    public string subtitle;
    [TextArea(3, 6)] public string description;
    [TextArea(2, 4)] public string experienceTips;
}

[RequireComponent(typeof(CanvasGroup))]
public class DefectInfoPanel : MonoBehaviour
{
    [Header("UI 文字參考")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI subtitleText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI tipsText;

    [Header("框內文字滾動參考")]
    [Tooltip("拖入內部的 TextScrollArea（掛有 ScrollRect 的子物件）")]
    public ScrollRect textScrollRect;

    [Header("智慧寬高與邊距設定")]
    public float preferredWidth = 380f;
    public float minSideWidth = 120f;
    [Tooltip("外框最大高度，內文超過此高度即啟用框內滾動")]
    public float maxHeight = 450f;
    [Tooltip("外框內除了 TextScrollArea 以外的上下保留高度（例如內距 + 固定標題高度）")]
    public float outerVerticalPadding = 40f;
    public float buttonSpacing = 16f;
    public float screenMargin = 16f;

    [Header("文本滾動速度（像素）")]
    public float mouseWheelPixels = 60f;
    public float rightStickPixelsPerSec = 350f;

    [Header("動畫設定")]
    public float fadeDuration = 0.15f;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private RectTransform parentRect;
    private Coroutine animCoroutine;
    private readonly Vector3[] btnWorldCorners = new Vector3[4];

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
        parentRect = rectTransform.parent as RectTransform;

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        ForceWrap(titleText);
        ForceWrap(subtitleText);
        ForceWrap(descriptionText);
        ForceWrap(tipsText);

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    void Update()
    {
        if (canvasGroup.alpha < 0.1f || textScrollRect == null) return;

        RectTransform content = textScrollRect.content;
        RectTransform viewport = textScrollRect.viewport != null
            ? textScrollRect.viewport
            : textScrollRect.GetComponent<RectTransform>();

        if (content == null || viewport == null) return;

        // 只有當文本高度大於可視框高度時才允許滾動
        float scrollableHeight = content.rect.height - viewport.rect.height;
        if (scrollableHeight <= 1f) return;

        float scrollPixels = 0f;

        // 1. 滑鼠滾輪
        if (Mouse.current != null)
        {
            float wheelY = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(wheelY) > 0.01f)
            {
                scrollPixels += Mathf.Sign(wheelY) * mouseWheelPixels;
            }
        }

        // 2. 手把右蘑菇頭
        if (Gamepad.current != null)
        {
            float stickY = Gamepad.current.rightStick.ReadValue().y;
            if (Mathf.Abs(stickY) > 0.15f)
            {
                scrollPixels += stickY * rightStickPixelsPerSec * Time.unscaledDeltaTime;
            }
        }

        // 將像素位移換算為正規化滾動進度 (1 = 頂部, 0 = 底部)
        if (Mathf.Abs(scrollPixels) > 0.01f)
        {
            float normalizedDelta = scrollPixels / scrollableHeight;
            textScrollRect.verticalNormalizedPosition = Mathf.Clamp01(
                textScrollRect.verticalNormalizedPosition + normalizedDelta
            );
        }
    }

    private void ForceWrap(TextMeshProUGUI tmp)
    {
        if (tmp == null) return;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
    }

    public void ShowInfo(DefectInfoData data, RectTransform targetButton)
    {
        if (data == null || targetButton == null || parentRect == null) return;

        // 1. 更新文字
        if (titleText != null) titleText.text = data.title;
        if (subtitleText != null) subtitleText.text = data.subtitle;
        if (descriptionText != null) descriptionText.text = data.description;
        if (tipsText != null) tipsText.text = data.experienceTips;

        // 2. 取得按鈕在父容器內的邊界
        targetButton.GetWorldCorners(btnWorldCorners);
        float btnMinX = float.MaxValue, btnMaxX = float.MinValue;
        float btnMinY = float.MaxValue, btnMaxY = float.MinValue;

        for (int i = 0; i < 4; i++)
        {
            Vector3 lp = parentRect.InverseTransformPoint(btnWorldCorners[i]);
            if (lp.x < btnMinX) btnMinX = lp.x;
            if (lp.x > btnMaxX) btnMaxX = lp.x;
            if (lp.y < btnMinY) btnMinY = lp.y;
            if (lp.y > btnMaxY) btnMaxY = lp.y;
        }

        Rect bounds = parentRect.rect;
        float minX = bounds.xMin + screenMargin;
        float maxX = bounds.xMax - screenMargin;
        float minY = bounds.yMin + screenMargin;
        float maxY = bounds.yMax - screenMargin;

        // 3. 計算左右剩餘空間與外框寬度
        float spaceToMaxX = maxX - (btnMaxX + buttonSpacing);
        float spaceToMinX = (btnMinX - buttonSpacing) - minX;

        bool placeTowardMaxX = spaceToMaxX >= spaceToMinX;
        float availableW = placeTowardMaxX ? spaceToMaxX : spaceToMinX;

        float finalWidth = Mathf.Clamp(availableW, minSideWidth, preferredWidth);
        if (availableW < minSideWidth) finalWidth = Mathf.Max(80f, availableW);

        // 4. 依內文高度調整外框高度（超過 maxHeight 則鎖定外框高度，改由內部滾動）
        float maxScreenHeight = Mathf.Max(150f, maxY - minY);
        float clampedMaxH = Mathf.Min(maxHeight, maxScreenHeight);
        float finalHeight = ApplyWidthAndGetClampedHeight(finalWidth, clampedMaxH);

        // 5. 定位外框
        float targetX = placeTowardMaxX
            ? btnMaxX + buttonSpacing + finalWidth * 0.5f
            : btnMinX - buttonSpacing - finalWidth * 0.5f;

        float btnCenterY = (btnMinY + btnMaxY) * 0.5f;
        float halfH = finalHeight * 0.5f;
        float targetY = (minY + halfH <= maxY - halfH)
            ? Mathf.Clamp(btnCenterY, minY + halfH, maxY - halfH)
            : (minY + maxY) * 0.5f;

        rectTransform.localPosition = new Vector3(targetX, targetY, 0f);

        // 6. 切換按鈕時，將內部文本滾回最頂端
        if (textScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            textScrollRect.verticalNormalizedPosition = 1f;
        }

        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(FadeIn());
    }

    private float ApplyWidthAndGetClampedHeight(float width, float maxAllowedHeight)
    {
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        Canvas.ForceUpdateCanvases();

        if (textScrollRect != null && textScrollRect.content != null)
        {
            RectTransform content = textScrollRect.content;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            if (titleText != null) titleText.ForceMeshUpdate();
            if (subtitleText != null) subtitleText.ForceMeshUpdate();
            if (descriptionText != null) descriptionText.ForceMeshUpdate();
            if (tipsText != null) tipsText.ForceMeshUpdate();

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            float desiredHeight = content.rect.height + outerVerticalPadding;
            float finalHeight = Mathf.Min(desiredHeight, maxAllowedHeight);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, finalHeight);
            return finalHeight;
        }

        return rectTransform.rect.height;
    }

    private IEnumerator FadeIn()
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }
}