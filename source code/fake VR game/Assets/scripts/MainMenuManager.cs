using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems; // 必須引入事件系統，才能控制焦點

public class MainMenuManager : MonoBehaviour
{
    [Header("UI 面板參考")]
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;

    [Header("搖桿焦點參考 (EventSystem)")]
    public GameObject firstSelectedMainMenu; // 主選單的第一個焦點 (如：開始遊戲按鈕)
    public GameObject firstSelectedSettings; // 設定選單的第一個焦點 (如：音量 Slider)

    [Header("設定選單 UI 參考")]
    public Slider volumeSlider;
    public Slider gyroSensitivitySlider;
    public Slider joystickSensitivitySlider;
    public Toggle touchToggle;
    public Toggle debugToggle;

    void Start()
    {
        // 確保初始狀態為主選單
        ShowMainMenu();
        // 載入玩家先前儲存的設定
        LoadSettings();
    }

    // --- 面板切換與焦點控制 ---

    public void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        settingsPanel.SetActive(false);
        // 切換回主選單時，將搖桿焦點重新設定給「開始遊戲」按鈕
        EventSystem.current.SetSelectedGameObject(firstSelectedMainMenu);
    }

    public void ShowSettings()
    {
        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(true);
        // 切換到設定選單時，將搖桿焦點設定給第一個選項 (如：音量 Slider)
        EventSystem.current.SetSelectedGameObject(firstSelectedSettings);
    }

    // --- 主選單功能 ---

    public void StartGame()
    {
        SaveSettings(); // 開始前確保設定已儲存
        SceneManager.LoadScene(1);
    }

    public void QuitGame()
    {
        SaveSettings();
        Application.Quit();
    }

    // --- 設定值儲存與讀取 (PlayerPrefs) ---

    public void SaveSettings()
    {
        // 將 UI 上的數值儲存到 PlayerPrefs
        PlayerPrefs.SetFloat("Volume", volumeSlider.value);
        PlayerPrefs.SetFloat("GyroSensitivity", gyroSensitivitySlider.value);
        PlayerPrefs.SetFloat("JoystickSensitivity", joystickSensitivitySlider.value);
        // Toggle 的 bool 值需轉為 int 儲存 (1=true, 0=false)
        PlayerPrefs.SetInt("TouchEnabled", touchToggle.isOn ? 1 : 0);
        PlayerPrefs.SetInt("DebugMode", debugToggle.isOn ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log("設定已儲存");
    }

    private void LoadSettings()
    {
        // 讀取 PlayerPrefs 的設定，若無則套用預設值 (第二個參數)
        volumeSlider.value = PlayerPrefs.GetFloat("Volume", 1.0f);
        gyroSensitivitySlider.value = PlayerPrefs.GetFloat("GyroSensitivity", 1.0f);
        joystickSensitivitySlider.value = PlayerPrefs.GetFloat("JoystickSensitivity", 1.0f);
        touchToggle.isOn = PlayerPrefs.GetInt("TouchEnabled", 0) == 1;
        debugToggle.isOn = PlayerPrefs.GetInt("DebugMode", 0) == 1;
    }
}