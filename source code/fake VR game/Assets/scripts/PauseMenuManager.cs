using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class PauseMenuManager : MonoBehaviour
{
    [Header("UI 面板參考")]
    public GameObject pauseMenuPanel;
    public GameObject settingsPanel;

    [Header("搖桿焦點設定")]
    public GameObject firstSelectedPause;    // 拖入 Btn_Resume
    public GameObject firstSelectedSettings; // 拖入設定面板的第一個元件

    [Header("輸入綁定")]
    public InputActionReference pauseAction; // 綁定剛才建立的 Pause Action (Gamepad Start)

    private bool isPaused = false;
    private bool isDebugMode;

    void Start()
    {
        // 確保遊戲開始時選單是隱藏的，時間是正常流動的
        pauseMenuPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
        Time.timeScale = 1f;

        // 讀取是否開啟了 Debug Mode
        isDebugMode = PlayerPrefs.GetInt("DebugMode", 0) == 1;
    }

    void OnEnable()
    {
        if (pauseAction != null)
        {
            pauseAction.action.Enable();
            pauseAction.action.performed += ctx => TogglePause();
        }
    }

    void OnDisable()
    {
        if (pauseAction != null)
        {
            pauseAction.action.performed -= ctx => TogglePause();
            pauseAction.action.Disable();
        }
    }

    void Update()
    {
        // 【Debug 模式】：允許使用鍵盤 ESC 鍵來觸發暫停
        if (isDebugMode && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (isPaused) ResumeGame();
        else PauseGame();
    }

    // --- 選單控制邏輯 ---

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f; // 凍結遊戲時間 (物理、移動等基於 Time.deltaTime 的運算會停止)
        
        pauseMenuPanel.SetActive(true);
        if (settingsPanel) settingsPanel.SetActive(false);

        // 將搖桿焦點設定到「繼續遊戲」按鈕
        EventSystem.current.SetSelectedGameObject(firstSelectedPause);
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f; // 恢復遊戲時間
        
        pauseMenuPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
    }

    public void OpenSettings()
    {
        pauseMenuPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(true);
        
        // 將搖桿焦點轉移到設定面板
        EventSystem.current.SetSelectedGameObject(firstSelectedSettings);
    }

    public void CloseSettings()
    {
        if (settingsPanel) settingsPanel.SetActive(false);
        pauseMenuPanel.SetActive(true);
        
        // 將搖桿焦點轉回暫停選單
        EventSystem.current.SetSelectedGameObject(firstSelectedPause);
    }

    // --- 場景切換邏輯 ---

    public void ReturnToDefectSelect()
    {
        Time.timeScale = 1f; // 【重要】：換場景前必須把時間恢復，否則下個場景會卡死
        SceneManager.LoadScene(1); // 假設 Index 1 是 DefectSelectScene
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f; // 【重要】：換場景前必須把時間恢復
        SceneManager.LoadScene(0); // 假設 Index 0 是 MainMenu
    }
}