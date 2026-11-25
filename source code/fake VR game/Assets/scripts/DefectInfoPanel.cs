using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.InputSystem;
using TMPro;

[System.Serializable]
public class DefectInfoData
{
    public string title;
    public string subtitle;

    [Header("多媒體預覽（選填，若同時設定則優先播放影片）")]
    public Sprite previewImage;
    public VideoClip previewVideo;

    [Header("區塊小標題與內文（內文行首加 '## ' 可建立段落小標題）")]
    public string descriptionHeading = "▎ 生理與知覺機制";
    [TextArea(3, 8)] public string description;

    public string tipsHeading = "▎ 體驗觀察重點";
    [TextArea(2, 5)] public string experienceTips;
}

[RequireComponent(typeof(CanvasGroup))]
public class DefectInfoPanel : MonoBehaviour
{
    [Header("UI 文字參考")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI subtitleText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI tipsText;

    [Header("多媒體展示參考")]
    [Tooltip("包含 PreviewImage 與 PreviewVideo 的父物件")]
    public GameObject mediaContainer;
    public Image previewImageDisplay;
    public RawImage previewVideoDisplay;
    [Tooltip("圖片/影片在視窗內的最大高度上限")]
    public float maxMediaHeight = 220f;
    [Tooltip("左右內距總和 (例如 Left 20 + Right 20 = 40)，用於精準計算媒體比例高度")]
    public float horizontalPadding = 40f;

    [Header("小標題樣式設定")]
    public Color sectionHeadingColor = new Color(1f, 0.82f, 0.4f); // 暖金黃色
    public Color inlineHeadingColor = new Color(0.55f, 0.85f, 1f); // 淺天藍色

    [Header("框內文字滾動參考")]
    public ScrollRect textScrollRect;

    [Header("智慧寬高與邊距設定")]
    public float preferredWidth = 420f;
    public float minSideWidth = 150f;
    public float maxHeight = 450f;
    public float outerVerticalPadding = 40f;
    public float buttonSpacing = 16f;
    public float screenMargin = 20f;

    [Header("文本滾動速度（像素）")]
    public float mouseWheelPixels = 80f;
    public float rightStickPixelsPerSec = 400f;

    [Header("動畫設定")]
    public float fadeDuration = 0.15f;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private RectTransform parentRect;
    private LayoutElement mediaLayoutElement;
    private VideoPlayer videoPlayer;
    private RenderTexture videoRenderTexture;
    private Coroutine animCoroutine;
    private readonly Vector3[] btnWorldCorners = new Vector3[4];

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
        parentRect = rectTransform.parent as RectTransform;

        if (textScrollRect == null)
            textScrollRect = GetComponentInChildren<ScrollRect>(true);

        ContentSizeFitter selfFitter = GetComponent<ContentSizeFitter>();
        if (selfFitter != null) selfFitter.enabled = false;

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        SetupMediaComponents();

        ForceWrap(titleText);
        ForceWrap(subtitleText);
        ForceWrap(descriptionText);
        ForceWrap(tipsText);

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    private void SetupMediaComponents()
    {
        if (mediaContainer != null)
        {
            mediaLayoutElement = mediaContainer.GetComponent<LayoutElement>();
            if (mediaLayoutElement == null)
                mediaLayoutElement = mediaContainer.AddComponent<LayoutElement>();
        }

        if (previewVideoDisplay != null)
        {
            videoPlayer = previewVideoDisplay.GetComponent<VideoPlayer>();
            if (videoPlayer == null)
                videoPlayer = previewVideoDisplay.gameObject.AddComponent<VideoPlayer>();

            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None; // 預覽預設靜音
        }
    }

    void OnDestroy()
    {
        if (videoRenderTexture != null)
        {
            videoRenderTexture.Release();
            Destroy(videoRenderTexture);
        }
    }

    void Update()
    {
        if (canvasGroup.alpha < 0.1f || textScrollRect == null) return;

        RectTransform content = textScrollRect.content;
        RectTransform viewport = textScrollRect.viewport != null
            ? textScrollRect.viewport
            : textScrollRect.GetComponent<RectTransform>();

        if (content == null || viewport == null) return;

        float scrollableHeight = content.rect.height - viewport.rect.height;
        if (scrollableHeight <= 1f) return;

        float scrollPixels = 0f;

        if (Mouse.current != null)
        {
            float wheelY = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(wheelY) > 0.01f)
                scrollPixels += Mathf.Sign(wheelY) * mouseWheelPixels;
        }

        if (Gamepad.current != null)
        {
            float stickY = Gamepad.current.rightStick.ReadValue().y;
            if (Mathf.Abs(stickY) > 0.15f)
                scrollPixels += stickY * rightStickPixelsPerSec * Time.unscaledDeltaTime;
        }

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
        tmp.richText = true;
    }

    public void ShowInfo(DefectInfoData data, RectTransform targetButton)
    {
        if (data == null || targetButton == null || parentRect == null) return;

        // 1. 填入標題與格式化小標題內文
        if (titleText != null) titleText.text = data.title;
        if (subtitleText != null) subtitleText.text = data.subtitle;
        if (descriptionText != null) descriptionText.text = FormatRichSection(data.descriptionHeading, data.description);
        if (tipsText != null) tipsText.text = FormatRichSection(data.tipsHeading, data.experienceTips);

        // 2. 取得按鈕四邊座標
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

        float spaceToMaxX = maxX - (btnMaxX + buttonSpacing);
        float spaceToMinX = (btnMinX - buttonSpacing) - minX;

        bool placeTowardMaxX = spaceToMaxX >= spaceToMinX;
        float availableW = placeTowardMaxX ? spaceToMaxX : spaceToMinX;

        float finalWidth = Mathf.Clamp(availableW, minSideWidth, preferredWidth);
        if (availableW < minSideWidth) finalWidth = Mathf.Max(80f, availableW);

        // 3. 更新圖片/影片並依當前寬度計算媒體高度
        UpdateMediaDisplay(data, finalWidth - horizontalPadding);

        // 4. 計算並限制外框總高度
        float maxScreenHeight = Mathf.Max(150f, maxY - minY);
        float clampedMaxH = Mathf.Min(maxHeight, maxScreenHeight);
        float finalHeight = ApplyWidthAndGetClampedHeight(finalWidth, clampedMaxH);

        float targetX = placeTowardMaxX
            ? btnMaxX + buttonSpacing + finalWidth * 0.5f
            : btnMinX - buttonSpacing - finalWidth * 0.5f;

        float btnCenterY = (btnMinY + btnMaxY) * 0.5f;
        float halfH = finalHeight * 0.5f;
        float targetY = (minY + halfH <= maxY - halfH)
            ? Mathf.Clamp(btnCenterY, minY + halfH, maxY - halfH)
            : (minY + maxY) * 0.5f;

        rectTransform.localPosition = new Vector3(targetX, targetY, 0f);

        if (textScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            textScrollRect.verticalNormalizedPosition = 1f;
        }

        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(FadeIn());
        StartCoroutine(ResetScrollTopNextFrame());
    }

    private void UpdateMediaDisplay(DefectInfoData data, float contentWidth)
    {
        if (mediaContainer == null) return;

        bool hasVideo = data.previewVideo != null && previewVideoDisplay != null && videoPlayer != null;
        bool hasImage = !hasVideo && data.previewImage != null && previewImageDisplay != null;

        if (!hasVideo && !hasImage)
        {
            if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Stop();
            mediaContainer.SetActive(false);
            return;
        }

        mediaContainer.SetActive(true);
        float aspect = 16f / 9f;

        if (hasVideo)
        {
            if (previewImageDisplay != null) previewImageDisplay.gameObject.SetActive(false);
            previewVideoDisplay.gameObject.SetActive(true);

            int vidW = data.previewVideo.width > 0 ? (int)data.previewVideo.width : 1280;
            int vidH = data.previewVideo.height > 0 ? (int)data.previewVideo.height : 720;
            aspect = (float)vidW / vidH;

            if (videoRenderTexture == null || videoRenderTexture.width != vidW || videoRenderTexture.height != vidH)
            {
                if (videoRenderTexture != null)
                {
                    videoRenderTexture.Release();
                    Destroy(videoRenderTexture);
                }
                videoRenderTexture = new RenderTexture(vidW, vidH, 0);
                videoRenderTexture.Create();
            }

            videoPlayer.targetTexture = videoRenderTexture;
            previewVideoDisplay.texture = videoRenderTexture;
            videoPlayer.clip = data.previewVideo;
            videoPlayer.Play();
        }
        else if (hasImage)
        {
            if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Stop();
            if (previewVideoDisplay != null) previewVideoDisplay.gameObject.SetActive(false);

            previewImageDisplay.gameObject.SetActive(true);
            previewImageDisplay.sprite = data.previewImage;

            if (data.previewImage.rect.height > 0f)
                aspect = data.previewImage.rect.width / data.previewImage.rect.height;
        }

        if (mediaLayoutElement != null)
        {
            float idealHeight = Mathf.Max(80f, contentWidth) / Mathf.Max(0.2f, aspect);
            mediaLayoutElement.preferredHeight = Mathf.Min(idealHeight, maxMediaHeight);
        }
    }

    private string FormatRichSection(string sectionHeading, string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody)) return string.Empty;

        string secHex = ColorUtility.ToHtmlStringRGB(sectionHeadingColor);
        string subHex = ColorUtility.ToHtmlStringRGB(inlineHeadingColor);
        StringBuilder sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(sectionHeading))
        {
            sb.AppendLine($"<size=115%><b><color=#{secHex}>{sectionHeading}</color></b></size>");
        }

        string[] lines = rawBody.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd('\r');
            if (line.StartsWith("## "))
            {
                string subTitle = line.Substring(3);
                if (i > 0) sb.AppendLine();
                sb.AppendLine($"<size=108%><b><color=#{subHex}>● {subTitle}</color></b></size>");
            }
            else
            {
                sb.AppendLine(line);
            }
        }

        return sb.ToString().TrimEnd();
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

        float fallbackHeight = Mathf.Min(rectTransform.rect.height, maxAllowedHeight);
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, fallbackHeight);
        return fallbackHeight;
    }

    private IEnumerator ResetScrollTopNextFrame()
    {
        yield return null;
        if (textScrollRect != null && textScrollRect.content != null)
        {
            textScrollRect.StopMovement();
            Canvas.ForceUpdateCanvases();
            textScrollRect.content.anchoredPosition = new Vector2(textScrollRect.content.anchoredPosition.x, 0f);
            textScrollRect.verticalNormalizedPosition = 1f;
        }
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