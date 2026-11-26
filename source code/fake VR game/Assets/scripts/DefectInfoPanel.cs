using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.InputSystem;
using TMPro;

public enum DefectMediaType
{
    Image,
    Video
}

[System.Serializable]
public class DefectMediaItem
{
    [Tooltip("媒體類型（若只拖入圖片或影片其中一項，程式也會自動辨識）")]
    public DefectMediaType type = DefectMediaType.Image;
    public Sprite image;
    public VideoClip video;
    [Tooltip("選填：顯示在此圖片或影片下方的簡短圖說")]
    public string caption;
}

[System.Serializable]
public class DefectInfoData
{
    public string title;
    public string subtitle;

    [Header("多媒體展示清單（可按 + 新增多張圖片或影片）")]
    public List<DefectMediaItem> mediaItems = new List<DefectMediaItem>();

    // 相容先前已綁定的單一媒體欄位（若 mediaItems 為空時自動讀取）
    [HideInInspector] public Sprite previewImage;
    [HideInInspector] public VideoClip previewVideo;

    [Header("區塊小標題與內文（內文行首加 '## ' 可建立段落小標題）")]
    public string descriptionHeading = "▎ 生理與知覺機制";
    [TextArea(3, 8)] public string description;

    public string tipsHeading = "▎ 體驗觀察重點";
    [TextArea(2, 5)] public string experienceTips;
}

[RequireComponent(typeof(CanvasGroup))]
public class DefectInfoPanel : MonoBehaviour
{
    private class MediaSlotInstance
    {
        public GameObject rootObj;
        public LayoutElement layoutElement;
        public RectTransform mediaFrameRect;
        public Image imageView;
        public AspectRatioFitter imageFitter;
        public RawImage videoView;
        public AspectRatioFitter videoFitter;
        public VideoPlayer videoPlayer;
        public RenderTexture renderTexture;
        public TextMeshProUGUI captionText;
    }

    [Header("UI 文字參考")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI subtitleText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI tipsText;

    [Header("多媒體展示設定")]
    [Tooltip("若有舊版單一 MediaContainer 可拖入，啟動時會自動隱藏改由多媒體清單接管")]
    public GameObject legacyMediaContainer;
    [Tooltip("單張圖片/影片的最大顯示高度上限")]
    public float maxMediaHeight = 360f;
    [Tooltip("左右內距總和 (例如 Left 20 + Right 20 = 40)，用於計算精準長寬比")]
    public float horizontalPadding = 40f;
    public float captionFontSize = 22f;
    public Color captionColor = new Color(0.82f, 0.85f, 0.9f, 1f);

    [Header("小標題樣式設定")]
    public Color sectionHeadingColor = new Color(1f, 0.82f, 0.4f);
    public Color inlineHeadingColor = new Color(0.55f, 0.85f, 1f);

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
    private Coroutine animCoroutine;
    private readonly Vector3[] btnWorldCorners = new Vector3[4];
    private readonly List<MediaSlotInstance> mediaPool = new List<MediaSlotInstance>();

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
        parentRect = rectTransform.parent as RectTransform;

        if (textScrollRect == null)
            textScrollRect = GetComponentInChildren<ScrollRect>(true);

        ContentSizeFitter selfFitter = GetComponent<ContentSizeFitter>();
        if (selfFitter != null) selfFitter.enabled = false;

        if (legacyMediaContainer != null)
            legacyMediaContainer.SetActive(false);

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

    void OnDestroy()
    {
        for (int i = 0; i < mediaPool.Count; i++)
        {
            if (mediaPool[i].renderTexture != null)
            {
                mediaPool[i].renderTexture.Release();
                Destroy(mediaPool[i].renderTexture);
            }
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

        // 3. 依當前寬度建立並更新所有多媒體項目（鎖定長寬比，絕不形變）
        UpdateMultipleMediaDisplays(data, finalWidth - horizontalPadding);

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

    private void UpdateMultipleMediaDisplays(DefectInfoData data, float contentWidth)
    {
        // 先停用並隱藏所有已生成的媒體槽位
        for (int i = 0; i < mediaPool.Count; i++)
        {
            if (mediaPool[i].videoPlayer != null && mediaPool[i].videoPlayer.isPlaying)
                mediaPool[i].videoPlayer.Stop();
            mediaPool[i].rootObj.SetActive(false);
        }

        if (textScrollRect == null || textScrollRect.content == null) return;

        // 整理有效的媒體清單（優先使用 mediaItems，若為空則相容讀取舊欄位）
        List<DefectMediaItem> validItems = new List<DefectMediaItem>();
        if (data.mediaItems != null && data.mediaItems.Count > 0)
        {
            for (int i = 0; i < data.mediaItems.Count; i++)
            {
                var item = data.mediaItems[i];
                if (item != null && (item.image != null || item.video != null))
                    validItems.Add(item);
            }
        }
        else if (data.previewVideo != null || data.previewImage != null)
        {
            validItems.Add(new DefectMediaItem
            {
                type = data.previewVideo != null ? DefectMediaType.Video : DefectMediaType.Image,
                image = data.previewImage,
                video = data.previewVideo
            });
        }

        if (validItems.Count == 0) return;

        float safeWidth = Mathf.Max(80f, contentWidth);

        for (int i = 0; i < validItems.Count; i++)
        {
            DefectMediaItem item = validItems[i];
            MediaSlotInstance slot = GetOrCreateMediaSlot(i, textScrollRect.content);

            slot.rootObj.SetActive(true);
            slot.rootObj.transform.SetSiblingIndex(i); // 依序排在 TextContent 最上方

            // 自動判斷該顯示影片還是圖片
            bool useVideo = (item.type == DefectMediaType.Video && item.video != null)
                            || (item.video != null && item.image == null);

            float aspect = 16f / 9f;

            if (useVideo)
            {
                slot.imageView.gameObject.SetActive(false);
                slot.videoView.gameObject.SetActive(true);

                int vidW = item.video.width > 0 ? (int)item.video.width : 1280;
                int vidH = item.video.height > 0 ? (int)item.video.height : 720;
                aspect = Mathf.Max(0.2f, (float)vidW / vidH);

                if (slot.renderTexture == null || slot.renderTexture.width != vidW || slot.renderTexture.height != vidH)
                {
                    if (slot.renderTexture != null)
                    {
                        slot.renderTexture.Release();
                        Destroy(slot.renderTexture);
                    }
                    slot.renderTexture = new RenderTexture(vidW, vidH, 0);
                    slot.renderTexture.Create();
                }

                // 鎖定影片原始長寬比，杜絕形變
                slot.videoFitter.aspectRatio = aspect;
                slot.videoPlayer.targetTexture = slot.renderTexture;
                slot.videoView.texture = slot.renderTexture;
                slot.videoPlayer.clip = item.video;
                slot.videoPlayer.Play();
            }
            else
            {
                slot.videoView.gameObject.SetActive(false);
                slot.imageView.gameObject.SetActive(true);
                slot.imageView.sprite = item.image;
                slot.imageView.preserveAspect = true;

                if (item.image.rect.height > 0f)
                    aspect = Mathf.Max(0.2f, item.image.rect.width / item.image.rect.height);

                // 鎖定圖片原始長寬比，杜絕形變
                slot.imageFitter.aspectRatio = aspect;
            }

            // 計算媒體顯示高度（維持精準比例且不超過 maxMediaHeight）
            float idealMediaHeight = safeWidth / aspect;
            float finalMediaHeight = Mathf.Min(idealMediaHeight, maxMediaHeight);

            // 處理選填的圖說文字
            bool hasCaption = !string.IsNullOrWhiteSpace(item.caption);
            float captionHeight = 0f;

            if (hasCaption)
            {
                slot.captionText.gameObject.SetActive(true);
                slot.captionText.text = item.caption;
                slot.captionText.ForceMeshUpdate();
                captionHeight = slot.captionText.GetPreferredValues(safeWidth, 0f).y + 8f;

                slot.mediaFrameRect.offsetMin = new Vector2(0f, captionHeight);
                slot.mediaFrameRect.offsetMax = Vector2.zero;
                slot.captionText.rectTransform.sizeDelta = new Vector2(0f, captionHeight);
            }
            else
            {
                slot.captionText.gameObject.SetActive(false);
                slot.mediaFrameRect.offsetMin = Vector2.zero;
                slot.mediaFrameRect.offsetMax = Vector2.zero;
            }

            slot.layoutElement.preferredHeight = finalMediaHeight + captionHeight;
        }
    }

    private MediaSlotInstance GetOrCreateMediaSlot(int index, RectTransform parentContent)
    {
        if (index < mediaPool.Count)
            return mediaPool[index];

        // 1. 建立單一媒體容器（由 TextContent 的 VerticalLayoutGroup 控制寬度）
        GameObject slotObj = new GameObject($"MediaSlot_{index}", typeof(RectTransform), typeof(LayoutElement));
        slotObj.transform.SetParent(parentContent, false);
        LayoutElement le = slotObj.GetComponent<LayoutElement>();

        // 2. 建立媒體畫面框（預留下方給圖說空間）
        GameObject frameObj = new GameObject("MediaFrame", typeof(RectTransform));
        frameObj.transform.SetParent(slotObj.transform, false);
        RectTransform frameRect = frameObj.GetComponent<RectTransform>();
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;

        // 3. 建立圖片顯示元件 + AspectRatioFitter（FitInParent 確保永不拉伸變形）
        GameObject imgObj = new GameObject("ImageView", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
        imgObj.transform.SetParent(frameRect, false);
        RectTransform imgRect = imgObj.GetComponent<RectTransform>();
        imgRect.anchorMin = Vector2.zero;
        imgRect.anchorMax = Vector2.one;
        imgRect.offsetMin = Vector2.zero;
        imgRect.offsetMax = Vector2.zero;

        Image img = imgObj.GetComponent<Image>();
        img.preserveAspect = true;
        img.maskable = true;
        img.raycastTarget = false;

        AspectRatioFitter imgFitter = imgObj.GetComponent<AspectRatioFitter>();
        imgFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        // 4. 建立影片顯示元件 + AspectRatioFitter + 獨立 VideoPlayer
        GameObject vidObj = new GameObject("VideoView", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter), typeof(VideoPlayer));
        vidObj.transform.SetParent(frameRect, false);
        RectTransform vidRect = vidObj.GetComponent<RectTransform>();
        vidRect.anchorMin = Vector2.zero;
        vidRect.anchorMax = Vector2.one;
        vidRect.offsetMin = Vector2.zero;
        vidRect.offsetMax = Vector2.zero;

        RawImage rawImg = vidObj.GetComponent<RawImage>();
        rawImg.maskable = true;
        rawImg.raycastTarget = false;

        AspectRatioFitter vidFitter = vidObj.GetComponent<AspectRatioFitter>();
        vidFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        VideoPlayer vp = vidObj.GetComponent<VideoPlayer>();
        vp.playOnAwake = false;
        vp.isLooping = true;
        vp.renderMode = VideoRenderMode.RenderTexture;
        vp.audioOutputMode = VideoAudioOutputMode.None;

        // 5. 建立圖說文字（繼承 descriptionText 的中文字型，避免缺字）
        GameObject capObj = new GameObject("CaptionText", typeof(RectTransform), typeof(TextMeshProUGUI));
        capObj.transform.SetParent(slotObj.transform, false);
        RectTransform capRect = capObj.GetComponent<RectTransform>();
        capRect.anchorMin = new Vector2(0f, 0f);
        capRect.anchorMax = new Vector2(1f, 0f);
        capRect.pivot = new Vector2(0.5f, 0f);
        capRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI capTmp = capObj.GetComponent<TextMeshProUGUI>();
        if (descriptionText != null && descriptionText.font != null)
        {
            capTmp.font = descriptionText.font;
            capTmp.fontSharedMaterial = descriptionText.fontSharedMaterial;
        }
        capTmp.fontSize = captionFontSize;
        capTmp.color = captionColor;
        capTmp.alignment = TextAlignmentOptions.Center;
        capTmp.textWrappingMode = TextWrappingModes.Normal;
        capTmp.maskable = true;
        capTmp.raycastTarget = false;

        var instance = new MediaSlotInstance
        {
            rootObj = slotObj,
            layoutElement = le,
            mediaFrameRect = frameRect,
            imageView = img,
            imageFitter = imgFitter,
            videoView = rawImg,
            videoFitter = vidFitter,
            videoPlayer = vp,
            captionText = capTmp
        };

        mediaPool.Add(instance);
        return instance;
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