using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class PauseMenuManager : MonoBehaviour
{
    [Header("UI 面板參考")]
    public GameObject pauseMenuPanel;
    public GameObject settingsPanel;

    [Header("搖桿焦點設定")]
    public GameObject firstSelectedPause;    
    public GameObject firstSelectedSettings; 

    [Header("設定選單 UI 參考")]
    public Slider volumeSlider;
    public Slider gyroSensitivitySlider;
    public Slider joystickSensitivitySlider;
    public Toggle touchToggle;
    public Toggle debugToggle;

    [Header("即時套用目標參考 (新增)")]
    public GameObject touchCanvas;             // 拖入 TouchUICanvas
    public MagicWindowCamera cameraScript;     // 拖入 MainCamera
    public PlayerMovement playerScript;        // 拖入 Player

    [Header("輸入綁定")]
    public InputActionReference pauseAction;

    private bool isPaused = false;
    private bool isDebugMode;

    void Start()
    {
        pauseMenuPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
        Time.timeScale = 1f;

        LoadSettings();
    }

// 新增這個專屬的接收方法
    private void OnPausePerformed(InputAction.CallbackContext ctx)
    {
        TogglePause();
    }

    void OnEnable()
    {
        if (pauseAction != null && pauseAction.action != null)
        {
            pauseAction.action.Enable();
            // 改用具名函式綁定
            pauseAction.action.performed += OnPausePerformed; 
        }
    }

    void OnDisable()
    {
        if (pauseAction != null && pauseAction.action != null)
        {
            // 精準解除綁定，釋放記憶體與舊指標
            pauseAction.action.performed -= OnPausePerformed; 
        }
    }

    void Update()
    {
        if (isDebugMode && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (settingsPanel != null && settingsPanel.activeSelf)
        {
            CloseSettings();
            return;
        }

        if (isPaused) ResumeGame();
        else PauseGame();
    }

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f; 
        
        pauseMenuPanel.SetActive(true);
        if (settingsPanel) settingsPanel.SetActive(false);

        EventSystem.current.SetSelectedGameObject(firstSelectedPause);
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f; 
        
        pauseMenuPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
    }

    public void OpenSettings()
    {
        pauseMenuPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(true);
        
        EventSystem.current.SetSelectedGameObject(firstSelectedSettings);
    }

    public void CloseSettings()
    {
        SaveSettings(); // 關閉時儲存並即時套用
        
        if (settingsPanel) settingsPanel.SetActive(false);
        pauseMenuPanel.SetActive(true);
        
        EventSystem.current.SetSelectedGameObject(firstSelectedPause);
    }

    public void SaveSettings()
    {
        if (volumeSlider != null) PlayerPrefs.SetFloat("Volume", volumeSlider.value);
        if (gyroSensitivitySlider != null) PlayerPrefs.SetFloat("GyroSensitivity", gyroSensitivitySlider.value);
        if (joystickSensitivitySlider != null) PlayerPrefs.SetFloat("JoystickSensitivity", joystickSensitivitySlider.value);
        if (touchToggle != null) PlayerPrefs.SetInt("TouchEnabled", touchToggle.isOn ? 1 : 0);
        if (debugToggle != null) PlayerPrefs.SetInt("DebugMode", debugToggle.isOn ? 1 : 0);
        
        PlayerPrefs.Save();
        isDebugMode = PlayerPrefs.GetInt("DebugMode", 0) == 1;

        // 【關鍵修復】：即時套用最新設定到遊戲中
        ApplySettingsToGame();

        Debug.Log("遊戲內設定已儲存並即時套用");
    }

    private void ApplySettingsToGame()
    {
        // 1. 即時顯示或隱藏虛擬搖桿 UI
        if (touchCanvas != null && touchToggle != null)
        {
            touchCanvas.SetActive(touchToggle.isOn);
        }

        // 2. 通知相機與移動腳本更新靈敏度與觸控狀態
        if (cameraScript != null) cameraScript.RefreshSettings();
        if (playerScript != null) playerScript.RefreshSettings();
    }

    private void LoadSettings()
    {
        if (volumeSlider != null) volumeSlider.value = PlayerPrefs.GetFloat("Volume", 1.0f);
        if (gyroSensitivitySlider != null) gyroSensitivitySlider.value = PlayerPrefs.GetFloat("GyroSensitivity", 1.0f);
        if (joystickSensitivitySlider != null) joystickSensitivitySlider.value = PlayerPrefs.GetFloat("JoystickSensitivity", 1.0f);
        if (touchToggle != null) touchToggle.isOn = PlayerPrefs.GetInt("TouchEnabled", 0) == 1;
        if (debugToggle != null) debugToggle.isOn = PlayerPrefs.GetInt("DebugMode", 0) == 1;

        isDebugMode = PlayerPrefs.GetInt("DebugMode", 0) == 1;
    }

    public void ReturnToDefectSelect()
    {
        Time.timeScale = 1f; 
        SceneManager.LoadScene(1); 
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f; 
        SceneManager.LoadScene(0); 
    }
}