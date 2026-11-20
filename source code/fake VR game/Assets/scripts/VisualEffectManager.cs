using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem;

public class VisualEffectManager : MonoBehaviour
{
    [Header("Volume 參考")]
    public Volume colorBlindVolume;
    public Volume myopiaVolume;
    public Volume migraineVolume;
    public Volume ketamineVolume;
    public Volume hyperopiaVolume;
    public Volume shroomsVolume;

    [Header("3D 遮罩與相機參考")]
    public GameObject migraineMaskObject;
    [Tooltip("請將 Main Camera 拖曳到此欄位，用於控制 FOV 與飄浮位移")]
    public Camera mainCamera;

    [Header("一般切換設定")]
    public float transitionSpeed = 2.0f;

    [Header("K他命解離參數")]
    public float baseFOV = 60f;
    [Tooltip("FOV 變化的最大幅度 (模擬身體變大變小)")]
    public float maxFovShift = 25f;
    [Tooltip("空間變形的速率")]
    public float ketamineFovSpeed = 0.5f;
    [Tooltip("上下飄浮的公尺數")]
    public float floatAmplitude = 0.25f;
    [Tooltip("飄浮的速率")]
    public float floatSpeed = 1.2f;

    [Header("致幻蘑菇奇幻參數")]
    [Tooltip("色彩流動與變換的速度")]
    public float shroomHueSpeed = 0.5f;
    [Tooltip("環境與身體一起呼吸的速度")]
    public float shroomBreatheSpeed = 2.0f;
    [Tooltip("呼吸時視角縮放的幅度")]
    public float shroomFovAmplitude = 3f;

    private bool isColorBlindActive = false;
    private bool isMyopiaActive = false;
    private bool isMigraineActive = false;
    private bool isKetamineActive = false;
    private bool isHyperopiaActive = false;
    private bool isShroomsActive = false;

    private ColorAdjustments shroomsColor;

    void Start()
    {
        // 1. 預設將所有濾鏡權重與狀態歸零
        if (colorBlindVolume != null) colorBlindVolume.weight = 0f;
        if (myopiaVolume != null) myopiaVolume.weight = 0f;
        if (migraineVolume != null) migraineVolume.weight = 0f;
        if (ketamineVolume != null) ketamineVolume.weight = 0f;
        if (hyperopiaVolume != null) hyperopiaVolume.weight = 0f;

        if (shroomsVolume != null)
        {
            shroomsVolume.weight = 0f;
            shroomsVolume.profile.TryGet(out shroomsColor);
        }

        if (migraineMaskObject != null) migraineMaskObject.SetActive(false);

        ReversePhiRenderFeature.isGlobalActive = false;

        // 2. 讀取玩家在選擇場景設定的障礙類型 (0 = 正常)
        int selectedDefect = PlayerPrefs.GetInt("SelectedDefect", 0);
        Debug.Log("遊戲開始，載入障礙模式：" + selectedDefect);

        // 3. 根據編號啟用對應狀態
        switch (selectedDefect)
        {
            case 1:
                isColorBlindActive = true;
                break;
            case 2:
                isMyopiaActive = true;
                break;
            case 3:
                isMigraineActive = true;
                if (migraineMaskObject != null) migraineMaskObject.SetActive(true);
                break;
            case 4:
                isKetamineActive = true;
                break;
            case 5:
                isHyperopiaActive = true;
                break;
            case 6:
                isShroomsActive = true;
                break;
            case 7:
                ReversePhiRenderFeature.isGlobalActive = true;
                break;
        }
    }

    void OnDisable()
    {
        // 離開場景或結束測試時，確保靜態開關歸零，避免殘留到選單場景
        ReversePhiRenderFeature.isGlobalActive = false;
    }

    void Update()
    {
        // 1. 一般 Volume 權重平滑漸變
        UpdateVolumeWeight(colorBlindVolume, isColorBlindActive);
        UpdateVolumeWeight(myopiaVolume, isMyopiaActive);
        UpdateVolumeWeight(migraineVolume, isMigraineActive);
        UpdateVolumeWeight(hyperopiaVolume, isHyperopiaActive);
        UpdateVolumeWeight(shroomsVolume, isShroomsActive);

        // 2. 動態運算 FOV 與位移 (相容 K他命與致幻蘑菇疊加)
        float currentTargetFOV = baseFOV;
        Vector3 currentLocalPos = Vector3.zero;

        // 【K他命解離邏輯】
        if (ketamineVolume != null)
        {
            float targetKWeight = isKetamineActive ? 1.0f : 0.0f;
            ketamineVolume.weight = Mathf.MoveTowards(ketamineVolume.weight, targetKWeight, transitionSpeed * Time.deltaTime);

            if (ketamineVolume.weight > 0.01f)
            {
                currentTargetFOV += Mathf.Sin(Time.time * ketamineFovSpeed) * maxFovShift * ketamineVolume.weight;

                float floatY = Mathf.Sin(Time.time * floatSpeed) * floatAmplitude * ketamineVolume.weight;
                float floatX = Mathf.Cos(Time.time * floatSpeed * 0.7f) * (floatAmplitude * 0.6f) * ketamineVolume.weight;
                currentLocalPos = new Vector3(floatX, floatY, 0f);
            }
        }

        // 【致幻蘑菇邏輯】
        if (shroomsVolume != null && shroomsVolume.weight > 0.01f)
        {
            currentTargetFOV += Mathf.Sin(Time.time * shroomBreatheSpeed) * shroomFovAmplitude * shroomsVolume.weight;

            if (shroomsColor != null)
            {
                shroomsColor.hueShift.value = Mathf.Sin(Time.time * shroomHueSpeed) * 180f * shroomsVolume.weight;
            }
        }
        else if (shroomsColor != null)
        {
            shroomsColor.hueShift.value = 0f;
        }

        // 套用相機變化 (遊戲暫停時不強行覆寫)
        if (mainCamera != null && Time.timeScale > 0f)
        {
            mainCamera.fieldOfView = currentTargetFOV;
            mainCamera.transform.localPosition = currentLocalPos;
        }

        // 【Debug 測試快捷鍵】
        if (Keyboard.current != null)
        {
            if (Keyboard.current.cKey.wasPressedThisFrame) ToggleColorBlind();
            if (Keyboard.current.mKey.wasPressedThisFrame) ToggleMyopia();
            if (Keyboard.current.vKey.wasPressedThisFrame) ToggleMigraineAura();
            if (Keyboard.current.kKey.wasPressedThisFrame) ToggleKetamine();
            if (Keyboard.current.hKey.wasPressedThisFrame) ToggleHyperopia();
            if (Keyboard.current.sKey.wasPressedThisFrame) ToggleShrooms();
            if (Keyboard.current.pKey.wasPressedThisFrame) ToggleReversePhi();
        }
    }

    private void UpdateVolumeWeight(Volume vol, bool isActive)
    {
        if (vol != null)
        {
            float targetWeight = isActive ? 1.0f : 0.0f;
            vol.weight = Mathf.MoveTowards(vol.weight, targetWeight, transitionSpeed * Time.deltaTime);
        }
    }

    public void ToggleColorBlind() => isColorBlindActive = !isColorBlindActive;
    public void ToggleMyopia() => isMyopiaActive = !isMyopiaActive;
    public void ToggleHyperopia() => isHyperopiaActive = !isHyperopiaActive;
    public void ToggleKetamine() => isKetamineActive = !isKetamineActive;
    public void ToggleShrooms() => isShroomsActive = !isShroomsActive;
    public void ToggleReversePhi() => ReversePhiRenderFeature.isGlobalActive = !ReversePhiRenderFeature.isGlobalActive;

    public void ToggleMigraineAura()
    {
        isMigraineActive = !isMigraineActive;
        if (migraineMaskObject != null) migraineMaskObject.SetActive(isMigraineActive);
    }

    public void ResetAllEffects()
    {
        isColorBlindActive = false;
        isMyopiaActive = false;
        isMigraineActive = false;
        isKetamineActive = false;
        isHyperopiaActive = false;
        isShroomsActive = false;
        ReversePhiRenderFeature.isGlobalActive = false;

        if (migraineMaskObject != null) migraineMaskObject.SetActive(false);

        if (mainCamera != null)
        {
            mainCamera.fieldOfView = baseFOV;
            mainCamera.transform.localPosition = Vector3.zero;
        }
    }
}