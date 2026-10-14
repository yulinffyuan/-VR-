using UnityEngine;

public class TouchUIManager : MonoBehaviour
{
    void Start()
    {
        // 讀取 PlayerPrefs 中的 TouchEnabled 設定 (1=開啟，0=關閉)
        // 預設給 0 (關閉)
        bool isTouchEnabled = PlayerPrefs.GetInt("TouchEnabled", 0) == 1;

        // 如果在設定中沒有打勾，就隱藏整個觸控介面；有打勾則顯示
        gameObject.SetActive(isTouchEnabled);
        
        if (isTouchEnabled)
        {
            Debug.Log("螢幕觸控模式已啟用，顯示虛擬搖桿。");
        }
    }
}