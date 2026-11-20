using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class DefectSelectManager : MonoBehaviour
{
    [Header("場景名稱設定")]
    public string gameSceneName = "GameScene";
    public string mainMenuSceneName = "MainMenu";

    [Header("搖桿預設焦點")]
    [Tooltip("進入此場景時，預設選中的第一個按鈕（會自動觸發該按鈕的浮動科普視窗）")]
    public GameObject firstSelectedButton;

    void Start()
    {
        // 確保時間軸正常、滑鼠解鎖，且全域錯覺特效已關閉
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ReversePhiRenderFeature.isGlobalActive = false;

        // 自動將搖桿焦點鎖定在第一個按鈕上
        if (firstSelectedButton != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstSelectedButton);
        }
    }

    /// <summary>
    /// 綁定至各障礙按鈕的 OnClick 事件，傳入對應的編號 (0~7)
    /// </summary>
    public void SelectDefectAndStart(int defectIndex)
    {
        PlayerPrefs.SetInt("SelectedDefect", defectIndex);
        PlayerPrefs.Save();
        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>
    /// 綁定至「返回主選單」按鈕的 OnClick 事件
    /// </summary>
    public void BackToMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }
}