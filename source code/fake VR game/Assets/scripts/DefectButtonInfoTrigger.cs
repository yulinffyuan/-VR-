using UnityEngine;
using UnityEngine.EventSystems;

public class DefectButtonInfoTrigger : MonoBehaviour, ISelectHandler, IPointerEnterHandler
{
    [Header("目標科普視窗")]
    public DefectInfoPanel infoPanel;

    [Header("此按鈕對應的科普內容")]
    public DefectInfoData infoData;

    private RectTransform buttonRect;

    void Awake()
    {
        buttonRect = GetComponent<RectTransform>();
    }

    public void OnSelect(BaseEventData eventData)
    {
        TriggerInfo();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        TriggerInfo();
    }

    private void TriggerInfo()
    {
        if (infoPanel != null)
        {
            infoPanel.ShowInfo(infoData, buttonRect);
        }
    }
}