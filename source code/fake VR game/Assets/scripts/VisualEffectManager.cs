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
    public Volume macularVolume; // 新增：黃斑部病變 Volume

    [Header("3D 遮罩與相機參考")]
    public GameObject migraineMaskObject;
    [Tooltip("請將 Main Camera 底下的 MacularScotomaMask 拖曳到此欄位")]
    public MacularScotomaController macularScotoma; // 新增：黃斑部中心暗點控制器
    [Tooltip("請將 Main Camera 拖曳到此欄位，用於控制 FOV 與飄浮位移")]
    public Camera mainCamera;

    [Header("一般切換設定")]
    public float transitionSpeed = 2.0f;

    [Header("K他命解離參數")]
    public float baseFOV = 60f;
    public float maxFovShift = 25f;
    public float ketamineFovSpeed = 0.5f;
    public float floatAmplitude = 0.25f;
    public float floatSpeed = 1.2f;

    [Header("致幻蘑菇奇幻參數")]
    public float shroomHueSpeed = 0.5f;
    public float shroomBreatheSpeed = 2.0f;
    public float shroomFovAmplitude = 3f;

    private bool isColorBlindActive = false;
    private bool isMyopiaActive = false;
    private bool isMigraineActive = false;
    private bool isKetamineActive = false;
    private bool isHyperopiaActive = false;
    private bool isShroomsActive = false;
    private bool isMacularActive = false; // 新增：黃斑部病變狀態

    private float macularWeight = 0f;
    private ColorAdjustments shroomsColor;

    void Start()
    {
        if (colorBlindVolume != null) colorBlindVolume.weight = 0f;
        if (myopiaVolume != null) myopiaVolume.weight = 0f;
        if (migraineVolume != null) migraineVolume.weight = 0f;
        if (ketamineVolume != null) ketamineVolume.weight = 0f;
        if (hyperopiaVolume != null) hyperopiaVolume.weight = 0f;
        if (macularVolume != null) macularVolume.weight = 0f;

        if (shroomsVolume != null)
        {
            shroomsVolume.weight = 0f;
            shroomsVolume.profile.TryGet(out shroomsColor);
        }

        if (migraineMaskObject != null) migraineMaskObject.SetActive(false);
        if (macularScotoma != null) macularScotoma.SetIntensity(0f);

        ReversePhiRenderFeature.isGlobalActive = false;

        int selectedDefect = PlayerPrefs.GetInt("SelectedDefect", 0);
        Debug.Log("遊戲開始，載入障礙模式：" + selectedDefect);

        switch (selectedDefect)
        {
            case 1: isColorBlindActive = true; break;
            case 2: isMyopiaActive = true; break;
            case 3:
                isMigraineActive = true;
                if (migraineMaskObject != null) migraineMaskObject.SetActive(true);
                break;
            case 4: isKetamineActive = true; break;
            case 5: isHyperopiaActive = true; break;
            case 6: isShroomsActive = true; break;
            case 7: ReversePhiRenderFeature.isGlobalActive = true; break;
            case 8: isMacularActive = true; break; // 編號 8 為黃斑部病變
        }
    }

    void OnDisable()
    {
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

        // 2. 黃斑部病變（Volume 與中心暗點遮罩同步漸變）
        float targetMacular = isMacularActive ? 1.0f : 0.0f;
        macularWeight = Mathf.MoveTowards(macularWeight, targetMacular, transitionSpeed * Time.deltaTime);
        if (macularVolume != null) macularVolume.weight = macularWeight;
        if (macularScotoma != null) macularScotoma.SetIntensity(macularWeight);

        // 3. 動態運算 FOV 與位移 (相容 K他命與致幻蘑菇疊加)
        float currentTargetFOV = baseFOV;
        Vector3 currentLocalPos = Vector3.zero;

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
            if (Keyboard.current.bKey.wasPressedThisFrame) ToggleMacularDegeneration(); // 按 B 鍵測試黃斑部病變
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
    public void ToggleMacularDegeneration() => isMacularActive = !isMacularActive;

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
        isMacularActive = false;
        macularWeight = 0f;
        ReversePhiRenderFeature.isGlobalActive = false;

        if (migraineMaskObject != null) migraineMaskObject.SetActive(false);
        if (macularScotoma != null) macularScotoma.SetIntensity(0f);

        if (mainCamera != null)
        {
            mainCamera.fieldOfView = baseFOV;
            mainCamera.transform.localPosition = Vector3.zero;
        }
    }
}