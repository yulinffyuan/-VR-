using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class DefectPromptOverride
{
    [Tooltip("對應 SelectedDefect 編號 (1:色盲, 2:近視, 3:偏頭痛, 4:K他命, 5:遠視, 6:致幻蘑菇, 7:ReversePhi, 8:黃斑部病變)")]
    public int defectId;
    public string customPrompt;
}

public class InteractableObject : MonoBehaviour
{
    [Header("互動狀態設定")]
    [Tooltip("是否允許玩家目前與此物體互動")]
    public bool isInteractable = true;

    [Header("動態提示文字設定")]
    [Tooltip("預設顯示在準心旁的動作文字（例如：檢視書本、拿起筆）")]
    public string defaultPrompt = "檢視物品";

    [Tooltip("可選：針對特定視知覺障礙模式顯示不同的提示文字")]
    public List<DefectPromptOverride> defectOverrides = new List<DefectPromptOverride>();

    [Header("互動觸發事件（按下手把 A 鍵後執行）")]
    public UnityEvent onInteract;

    private string runtimeOverridePrompt = null;

    /// <summary>
    /// 取得當下應該顯示在準心旁的互動提示文字
    /// </summary>
    public string GetCurrentPrompt()
    {
        // 1. 若任務系統有指定動態文字，最優先顯示
        if (!string.IsNullOrEmpty(runtimeOverridePrompt))
            return runtimeOverridePrompt;

        // 2. 檢查是否有針對目前視知覺障礙模式的專屬文字
        int currentDefect = PlayerPrefs.GetInt("SelectedDefect", 0);
        for (int i = 0; i < defectOverrides.Count; i++)
        {
            if (defectOverrides[i].defectId == currentDefect && !string.IsNullOrEmpty(defectOverrides[i].customPrompt))
            {
                return defectOverrides[i].customPrompt;
            }
        }

        // 3. 回傳預設文字
        return defaultPrompt;
    }

    /// <summary>
    /// 供未來的 TaskManager 動態修改此物件的提示文字（傳入 null 則恢復預設）
    /// </summary>
    public void SetRuntimePrompt(string prompt)
    {
        runtimeOverridePrompt = prompt;
    }

    /// <summary>
    /// 供未來的 TaskManager 動態開啟或關閉此物件的互動權限
    /// </summary>
    public void SetInteractable(bool state)
    {
        isInteractable = state;
    }

    /// <summary>
    /// 執行互動
    /// </summary>
    public void TriggerInteract()
    {
        if (!isInteractable) return;

        Debug.Log($"[互動觸發] 玩家與「{gameObject.name}」互動：{GetCurrentPrompt()}");
        onInteract?.Invoke();
    }
}