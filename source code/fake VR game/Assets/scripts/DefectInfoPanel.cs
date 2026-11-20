using System.Collections;
using UnityEngine;
using UnityEngine.UI;
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

    [Header("智慧寬度與邊距設定")]
    [Tooltip("空間充足時的理想寬度")]
    public float preferredWidth = 420f;
    [Tooltip("空間狹窄時的最小壓縮寬度")]
    public float minWidth = 240f;
    [Tooltip("視窗與按鈕之間的間距")]
    public float buttonSpacing = 20f;
    [Tooltip("視窗與螢幕四邊保留的安全邊距")]
    public float screenMargin = 20f;

    [Header("動畫設定")]
    public float fadeDuration = 0.18f;
    public float slideOffsetX = 12f;

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

        // 確保 Anchors 居中，方便統一使用局部座標計算
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    public void ShowInfo(DefectInfoData data, RectTransform targetButton)
    {
        if (data == null || targetButton == null || parentRect == null) return;

        // 1. 填入文字
        if (titleText != null) titleText.text = data.title;
        if (subtitleText != null) subtitleText.text = data.subtitle;
        if (descriptionText != null) descriptionText.text = data.description;
        if (tipsText != null) tipsText.text = data.experienceTips;

        // 2. 取得按鈕四個角在 Canvas 局部空間的座標 (0:左下, 1:左上, 2:右上, 3:右下)
        targetButton.GetWorldCorners(btnWorldCorners);
        Vector3 localBottomLeft = parentRect.InverseTransformPoint(btnWorldCorners[0]);
        Vector3 localTopRight = parentRect.InverseTransformPoint(btnWorldCorners[2]);

        float btnLeft = localBottomLeft.x;
        float btnRight = localTopRight.x;
        float btnCenterY = (localBottomLeft.y + localTopRight.y) * 0.5f;

        // 3. 取得螢幕(Canvas)可用邊界
        Rect canvasBounds = parentRect.rect;
        float minX = canvasBounds.xMin + screenMargin;
        float maxX = canvasBounds.xMax - screenMargin;
        float minY = canvasBounds.yMin + screenMargin;
        float maxY = canvasBounds.yMax - screenMargin;

        // 4. 計算按鈕左右兩側的剩餘可用寬度
        float spaceRight = maxX - (btnRight + buttonSpacing);
        float spaceLeft = (btnLeft - buttonSpacing) - minX;

        // 優先放右側；若右側不夠理想寬度且左側空間更大，則自動翻轉到左側
        bool placeOnRight = (spaceRight >= preferredWidth) || (spaceRight >= spaceLeft);
        float availableWidth = placeOnRight ? spaceRight : spaceLeft;

        // 5. 動態設定視窗寬度 (介於 minWidth 與 preferredWidth 之間)
        float finalWidth = Mathf.Clamp(availableWidth, minWidth, preferredWidth);
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, finalWidth);

        // 6. 強制重算排版，取得在該寬度與文字量下的真實高度
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        float finalHeight = rectTransform.rect.height;

        // 7. 依據左右側設定 Pivot 並計算最終座標
        float targetX;
        if (placeOnRight)
        {
            rectTransform.pivot = new Vector2(0f, 0.5f); // 軸心靠左，往右展開
            targetX = btnRight + buttonSpacing;
        }
        else
        {
            rectTransform.pivot = new Vector2(1f, 0.5f); // 軸心靠右，往左展開
            targetX = btnLeft - buttonSpacing;
        }

        // 8. 限制 Y 軸位置，確保視窗上下絕對不會超出螢幕邊緣
        float halfHeight = finalHeight * 0.5f;
        float clampedY = Mathf.Clamp(btnCenterY, minY + halfHeight, maxY - halfHeight);
        Vector2 targetPos = new Vector2(targetX, clampedY);

        // 9. 播放從按鈕旁微微滑出的動畫
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        float slideDir = placeOnRight ? -slideOffsetX : slideOffsetX;
        animCoroutine = StartCoroutine(PlayFloatInAnimation(targetPos, slideDir));
    }

    private IEnumerator PlayFloatInAnimation(Vector2 targetPos, float startOffsetX)
    {
        float elapsed = 0f;
        Vector2 startPos = targetPos + new Vector2(startOffsetX, 0f);

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            float ease = Mathf.SmoothStep(0f, 1f, t);

            canvasGroup.alpha = ease;
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, ease);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        rectTransform.anchoredPosition = targetPos;
    }
}