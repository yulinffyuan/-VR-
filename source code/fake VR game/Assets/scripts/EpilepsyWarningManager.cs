using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;

public class EpilepsyWarningManager : MonoBehaviour
{
    [Header("場景設定")]
    [Tooltip("主選單的場景名稱，需與 Build Profiles 完全一致")]
    public string mainMenuSceneName = "MainMenu";

    [Header("UI 綁定")]
    public Button agreeButton;
    public TextMeshProUGUI buttonText;

    [Header("防誤觸設定")]
    [Tooltip("強制閱讀秒數，倒數結束後按鈕才可點擊")]
    public float waitDuration = 3f;

    void Start()
    {
        // 確保時間軸正常
        Time.timeScale = 1f;

        if (agreeButton != null)
        {
            agreeButton.onClick.AddListener(OnAgreeClicked);
            StartCoroutine(EnableButtonAfterDelay());
        }
    }

    private IEnumerator EnableButtonAfterDelay()
    {
        agreeButton.interactable = false;

        float remaining = waitDuration;
        while (remaining > 0f)
        {
            if (buttonText != null)
            {
                buttonText.text = $"請詳閱上述警告 ({Mathf.CeilToInt(remaining)})";
            }
            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        // 倒數結束：啟用按鈕並自動將搖桿焦點鎖定在按鈕上
        agreeButton.interactable = true;
        if (buttonText != null)
        {
            buttonText.text = "我已了解並同意繼續 (按 A 鍵 / 點擊)";
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(agreeButton.gameObject);
        }
    }

    public void OnAgreeClicked()
    {
        agreeButton.interactable = false; // 防止連點重複載入
        SceneManager.LoadScene(mainMenuSceneName);
    }
}