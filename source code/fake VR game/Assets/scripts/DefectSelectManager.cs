using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class DefectSelectManager : MonoBehaviour
{
    [Header("搖桿焦點設定")]
    public GameObject firstSelectedButton; // 拖入 Btn_Normal

    void Start()
    {
        // 確保搖桿一進場景就有焦點
        if (firstSelectedButton != null)
        {
            EventSystem.current.SetSelectedGameObject(firstSelectedButton);
        }
    }

    /// <summary>
    /// 綁定到按鈕的 OnClick。
    /// 0:正常, 1:色盲, 2:近視, 3:偏頭痛
    /// </summary>
    public void SelectDefect(int defectType)
    {
        // 儲存玩家選擇的障礙類型
        PlayerPrefs.SetInt("SelectedDefect", defectType);
        PlayerPrefs.Save();
        
        Debug.Log("已選擇障礙模式：" + defectType);
        
        // 載入遊戲主場景 (Index 2)
        SceneManager.LoadScene(2);
    }
}