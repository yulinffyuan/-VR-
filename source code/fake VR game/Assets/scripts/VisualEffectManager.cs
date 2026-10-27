using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal; // 必須引入才能動態控制 URP 細部參數
using UnityEngine.InputSystem;

public class VisualEffectManager : MonoBehaviour
{
    [Header("Volume 參考")]
    public Volume colorBlindVolume;
    public Volume myopiaVolume;
    public Volume migraineVolume; 
    public Volume ketamineVolume; 
    public Volume hyperopiaVolume; 
    public Volume shroomsVolume; // 新增致幻蘑菇 Volume

    [Header("3D 遮罩與相機參考")]
    public GameObject migraineMaskObject; 
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
    [Tooltip("色彩流動與變換的速度")]
    public float shroomHueSpeed = 0.5f; 
    [Tooltip("環境與身體一起呼吸的速度")]
    public float shroomBreatheSpeed = 2.0f;
    [Tooltip("呼吸時視角縮放的幅度 (營造合一感)")]
    public float shroomFovAmplitude = 3f;

    private bool isColorBlindActive = false;
    private bool isMyopiaActive = false;
    private bool isMigraineActive = false; 
    private bool isKetamineActive = false; 
    private bool isHyperopiaActive = false; 
    private bool isShroomsActive = false; // 新增蘑菇狀態

    // 用來暫存蘑菇 Volume 裡的 ColorAdjustments，以便動態修改
    private ColorAdjustments shroomsColor; 

    void Start()
    {
        if (colorBlindVolume != null) colorBlindVolume.weight = 0f;
        if (myopiaVolume != null) myopiaVolume.weight = 0f;
        if (migraineVolume != null) migraineVolume.weight = 0f;
        if (ketamineVolume != null) ketamineVolume.weight = 0f;
        if (hyperopiaVolume != null) hyperopiaVolume.weight = 0f;
        
        if (shroomsVolume != null) 
        {
            shroomsVolume.weight = 0f;
            // 嘗試從 Profile 中取得 ColorAdjustments 組件
            shroomsVolume.profile.TryGet(out shroomsColor);
        }
        
        if (migraineMaskObject != null) migraineMaskObject.SetActive(false);

        int selectedDefect = PlayerPrefs.GetInt("SelectedDefect", 0);

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
            case 6: isShroomsActive = true; break; // 編號 6 為致幻蘑菇
        }
    }

    void Update()
    {
        // 1. 一般 Volume 權重漸變
        UpdateVolumeWeight(colorBlindVolume, isColorBlindActive);
        UpdateVolumeWeight(myopiaVolume, isMyopiaActive);
        UpdateVolumeWeight(migraineVolume, isMigraineActive);
        UpdateVolumeWeight(hyperopiaVolume, isHyperopiaActive); 
        UpdateVolumeWeight(shroomsVolume, isShroomsActive); 

        // 2. 動態運算 FOV 與 位移 (融合 K他命與蘑菇的狀態)
        float currentTargetFOV = baseFOV;
        Vector3 currentLocalPos = Vector3.zero;

        // 【K他命邏輯】
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

        // 【致幻蘑菇邏輯】：色彩扭曲與環境呼吸
        if (shroomsVolume != null && shroomsVolume.weight > 0.01f)
        {
            // 動態改變 FOV 產生規律、輕微的環境呼吸感 (比K他命快且輕柔)
            currentTargetFOV += Mathf.Sin(Time.time * shroomBreatheSpeed) * shroomFovAmplitude * shroomsVolume.weight;

            // 動態改變色相 (Hue Shift)，範圍在 -180 到 180 之間，產生色彩瘋狂流動疊加的幻覺
            if (shroomsColor != null)
            {
                shroomsColor.hueShift.value = Mathf.Sin(Time.time * shroomHueSpeed) * 180f * shroomsVolume.weight;
            }
        }
        else if (shroomsColor != null)
        {
            // 沒啟動時將色相歸零
            shroomsColor.hueShift.value = 0f;
        }

        // 應用相機變更
        if (mainCamera != null)
        {
            mainCamera.fieldOfView = currentTargetFOV;
            mainCamera.transform.localPosition = currentLocalPos;
        }

        // 【Debug 用】
        if (Keyboard.current != null)
        {
            if (Keyboard.current.cKey.wasPressedThisFrame) ToggleColorBlind();
            if (Keyboard.current.mKey.wasPressedThisFrame) ToggleMyopia();
            if (Keyboard.current.vKey.wasPressedThisFrame) ToggleMigraineAura(); 
            if (Keyboard.current.kKey.wasPressedThisFrame) ToggleKetamine(); 
            if (Keyboard.current.hKey.wasPressedThisFrame) ToggleHyperopia(); 
            if (Keyboard.current.sKey.wasPressedThisFrame) ToggleShrooms(); // 按 S 鍵測試蘑菇
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
    public void ToggleShrooms() => isShroomsActive = !isShroomsActive; // 蘑菇切換
    
    public void ToggleMigraineAura()
    {
        isMigraineActive = !isMigraineActive;
        if (migraineMaskObject != null) migraineMaskObject.SetActive(isMigraineActive);
    }
}