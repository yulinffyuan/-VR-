using UnityEngine;

public class MigraineMaskAnimator : MonoBehaviour
{
    [Header("動態閃爍設定")]
    public float flickerSpeed = 25f; // 鋸齒閃爍的頻率
    public float scaleSpeed = 1.5f;   // 暗點微幅縮放的頻率
    public float rotateSpeed = 5f;    // 暗點緩慢旋轉
    
    private Vector3 originalScale;
    private Material mat;

    void Start()
    {
        originalScale = transform.localScale;
        
        // 取得 Quad 的材質
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null) mat = renderer.material;
    }

    void Update()
    {
        // 1. 模擬偏頭痛「擴張與跳動」：微幅改變大小
        float scaleOffset = Mathf.Sin(Time.time * scaleSpeed) * 0.03f;
        transform.localScale = originalScale + new Vector3(scaleOffset, scaleOffset, 0);

        // 2. 模擬偏頭痛「緩慢游移」：微幅旋轉
        transform.Rotate(Vector3.forward * rotateSpeed * Time.deltaTime);

        // 3. 模擬偏頭痛「光芒閃爍 (Flickering)」：快速改變不透明度
        if (mat != null)
        {
            Color color = mat.color;
            // 讓透明度在 0.8 到 1.0 之間極快速跳動
            color.a = 0.85f + Mathf.Sin(Time.time * flickerSpeed) * 0.15f; 
            mat.color = color;
        }
    }
}