using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;

public class VisualEffectManager : MonoBehaviour
{
    [Header("Volume 參考")]
    public Volume colorBlindVolume;
    public Volume myopiaVolume;
    public Volume migraineVolume; 

    [Header("3D 遮罩物件參考")]
    [Tooltip("請將 Main Camera 底下的 MigraineAuraMask 拖曳到這裡")]
    public GameObject migraineMaskObject; // 新增遮罩物件參考

    [Header("切換設定")]
    public float transitionSpeed = 2.0f; 
    
    private bool isColorBlindActive = false;
    private bool isMyopiaActive = false;
    private bool isMigraineActive = false; 

void Start()
    {
        // 預設將所有權重歸零
        if (colorBlindVolume != null) colorBlindVolume.weight = 0f;
        if (myopiaVolume != null) myopiaVolume.weight = 0f;
        if (migraineVolume != null) migraineVolume.weight = 0f;
        if (migraineMaskObject != null) migraineMaskObject.SetActive(false);

        // 讀取玩家在選擇場景設定的障礙類型 (預設為 0 正常)
        int selectedDefect = PlayerPrefs.GetInt("SelectedDefect", 0);

        // 根據選擇直接啟用對應的布林值
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
            default:
                // 0 = 正常，不開啟任何濾鏡
                break;
        }
        
        Debug.Log("遊戲開始，載入障礙模式：" + selectedDefect);
    }
    void Update()
    {
        // 1. 平滑漸變邏輯：色盲
        if (colorBlindVolume != null)
        {
            float targetWeight = isColorBlindActive ? 1.0f : 0.0f;
            colorBlindVolume.weight = Mathf.MoveTowards(colorBlindVolume.weight, targetWeight, transitionSpeed * Time.deltaTime);
        }

        // 2. 平滑漸變邏輯：近視
        if (myopiaVolume != null)
        {
            float targetWeight = isMyopiaActive ? 1.0f : 0.0f;
            myopiaVolume.weight = Mathf.MoveTowards(myopiaVolume.weight, targetWeight, transitionSpeed * Time.deltaTime);
        }

        // 3. 平滑漸變邏輯：偏頭痛預兆 (Volume)
        if (migraineVolume != null)
        {
            float targetWeight = isMigraineActive ? 1.0f : 0.0f;
            migraineVolume.weight = Mathf.MoveTowards(migraineVolume.weight, targetWeight, transitionSpeed * Time.deltaTime);
        }

        // 【Debug 用】：鍵盤按鍵測試 (避開 WASD)
        if (Keyboard.current != null)
        {
            if (Keyboard.current.cKey.wasPressedThisFrame) ToggleColorBlind();
            if (Keyboard.current.mKey.wasPressedThisFrame) ToggleMyopia();
            if (Keyboard.current.vKey.wasPressedThisFrame) ToggleMigraineAura(); 
        }
    }

    // --- 切換方法供外部呼叫 ---

    public void ToggleColorBlind()
    {
        isColorBlindActive = !isColorBlindActive;
        Debug.Log("紅綠色盲狀態：" + (isColorBlindActive ? "開啟" : "關閉"));
    }

    public void ToggleMyopia()
    {
        isMyopiaActive = !isMyopiaActive;
        Debug.Log("近視狀態：" + (isMyopiaActive ? "開啟" : "關閉"));
    }

    public void ToggleMigraineAura()
    {
        isMigraineActive = !isMigraineActive;
        
        // 【關鍵】：切換偏頭痛狀態時，同步開啟或關閉前置遮罩物件
        if (migraineMaskObject != null)
        {
            migraineMaskObject.SetActive(isMigraineActive);
        }
        
        Debug.Log("偏頭痛視覺預兆狀態：" + (isMigraineActive ? "開啟" : "關閉"));
    }

    public void ResetAllEffects()
    {
        isColorBlindActive = false;
        isMyopiaActive = false;
        isMigraineActive = false;
        if (migraineMaskObject != null) migraineMaskObject.SetActive(false);
        Debug.Log("已重置所有視覺狀態");
    }
}