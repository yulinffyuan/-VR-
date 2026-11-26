using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class MacularScotomaController : MonoBehaviour
{
    [Header("Shader 參考（請拖入 MacularMetamorphopsia 避免打包遺失）")]
    public Shader macularShader;

    [Header("局部黃斑區範圍設定")]
    [Tooltip("受影響的中央黃斑區半徑（預設 0.22，外圍保持完全筆直清晰）")]
    [Range(0.05f, 0.45f)] public float distortRadius = 0.22f;

    [Header("視物變形 (Metamorphopsia) 參數")]
    [Tooltip("直線彎曲扭曲的強度（建議 0.02 ~ 0.045）")]
    [Range(0f, 0.08f)] public float distortStrength = 0.028f;
    [Tooltip("波浪扭曲的空間頻率（越高線條折曲越密集）")]
    [Range(5f, 60f)] public float waveFrequency = 26f;
    [Tooltip("細微動態起伏速度（設為 0 則為靜態扭曲）")]
    [Range(0f, 4f)] public float waveSpeed = 0.8f;

    [Header("局部中心模糊與暗點 (Scotoma)")]
    [Tooltip("中央區局部模糊程度")]
    [Range(0f, 0.015f)] public float blurAmount = 0.004f;
    [Tooltip("中央暗影/盲點半徑（設為 0 則純粹只有視物扭曲，沒有黑影）")]
    [Range(0f, 0.25f)] public float scotomaRadius = 0.08f;
    [Tooltip("中央暗影顏色與不透明度（Alpha 調低可讓患者隱約看見扭曲的輪廓）")]
    public Color scotomaColor = new Color(0.14f, 0.14f, 0.15f, 0.55f);

    private Material runtimeMat;
    private float currentIntensity = 0f;

    private static readonly int IntensityID = Shader.PropertyToID("_Intensity");
    private static readonly int AspectID = Shader.PropertyToID("_Aspect");
    private static readonly int DistortRadiusID = Shader.PropertyToID("_DistortRadius");
    private static readonly int DistortStrengthID = Shader.PropertyToID("_DistortStrength");
    private static readonly int WaveFrequencyID = Shader.PropertyToID("_WaveFrequency");
    private static readonly int WaveSpeedID = Shader.PropertyToID("_WaveSpeed");
    private static readonly int BlurAmountID = Shader.PropertyToID("_BlurAmount");
    private static readonly int ScotomaRadiusID = Shader.PropertyToID("_ScotomaRadius");
    private static readonly int ScotomaColorID = Shader.PropertyToID("_ScotomaColor");

    void Awake()
    {
        if (macularShader == null)
            macularShader = Shader.Find("Hidden/Custom/MacularMetamorphopsia");

        if (macularShader != null)
        {
            runtimeMat = new Material(macularShader);
            GetComponent<MeshRenderer>().material = runtimeMat;
        }
        else
        {
            Debug.LogError("[MacularScotomaController] 找不到 Hidden/Custom/MacularMetamorphopsia Shader！");
        }

        SetIntensity(0f);
    }

    void Update()
    {
        if (currentIntensity > 0.001f && runtimeMat != null)
        {
            SyncShaderParameters();
        }
    }

    private void SyncShaderParameters()
    {
        float aspect = (transform.localScale.y > 0.001f)
            ? (transform.localScale.x / transform.localScale.y)
            : 1.5f;

        runtimeMat.SetFloat(IntensityID, currentIntensity);
        runtimeMat.SetFloat(AspectID, aspect);
        runtimeMat.SetFloat(DistortRadiusID, distortRadius);
        runtimeMat.SetFloat(DistortStrengthID, distortStrength);
        runtimeMat.SetFloat(WaveFrequencyID, waveFrequency);
        runtimeMat.SetFloat(WaveSpeedID, waveSpeed);
        runtimeMat.SetFloat(BlurAmountID, blurAmount);
        runtimeMat.SetFloat(ScotomaRadiusID, scotomaRadius);
        runtimeMat.SetColor(ScotomaColorID, scotomaColor);
    }

    public void SetIntensity(float weight)
    {
        currentIntensity = Mathf.Clamp01(weight);

        if (currentIntensity <= 0.001f)
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
            return;
        }

        if (!gameObject.activeSelf) gameObject.SetActive(true);

        if (runtimeMat != null)
        {
            SyncShaderParameters();
        }
    }

    void OnDestroy()
    {
        if (runtimeMat != null) Destroy(runtimeMat);
    }
}