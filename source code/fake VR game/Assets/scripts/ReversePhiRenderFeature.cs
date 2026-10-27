using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule; 

public class ReversePhiRenderFeature : ScriptableRendererFeature
{
    public static bool isGlobalActive = false;

    class ReversePhiPass : ScriptableRenderPass
    {
        public Material material;
        public float blend;
        public Vector2 offset;
        
        private RTHandle historyHandle;

        public ReversePhiPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public void Dispose()
        {
            historyHandle?.Release();
            historyHandle = null;
        }

        class PassData
        {
            public TextureHandle source;
            public TextureHandle history;
            public RTHandle rawHistoryHandle; // 【關鍵修改】：直接保存 RTHandle 指標
            public Material material;
            public float blend;
            public Vector2 offset;
        }

        class CopyPassData
        {
            public TextureHandle source;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null || !isGlobalActive) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            TextureHandle activeColor = resourceData.activeColorTexture;
            var desc = cameraData.cameraTargetDescriptor;

            if (historyHandle == null || historyHandle.rt.width != desc.width || historyHandle.rt.height != desc.height)
            {
                historyHandle?.Release();
                historyHandle = RTHandles.Alloc(desc.width, desc.height, colorFormat: desc.graphicsFormat, name: "ReversePhiHistory");
            }

            TextureHandle historyTexture = renderGraph.ImportTexture(historyHandle);

            TextureDesc tempDesc = new TextureDesc(desc.width, desc.height);
            tempDesc.colorFormat = desc.graphicsFormat;
            tempDesc.name = "ReversePhiTemp";
            
            TextureHandle tempTexture = renderGraph.CreateTexture(tempDesc);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("ReversePhi_Render", out var passData))
            {
                passData.source = activeColor;
                passData.history = historyTexture;
                passData.rawHistoryHandle = historyHandle; // 傳遞真實的記憶體指標
                passData.material = material;
                passData.blend = blend;
                passData.offset = offset;

                builder.UseTexture(passData.source, AccessFlags.Read);
                builder.UseTexture(passData.history, AccessFlags.Read);
                builder.SetRenderAttachment(tempTexture, 0);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    data.material.SetFloat("_Blend", data.blend);
                    data.material.SetVector("_OffsetVector", data.offset);
                    
                    // 【終極修復】：將紋理直接賦予這個 Material 實體，完全避開 Global State 限制！
                    data.material.SetTexture("_HistoryTex", data.rawHistoryHandle);
                    
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>("ReversePhi_ApplyToScreen", out var passData))
            {
                passData.source = tempTexture;
                builder.UseTexture(passData.source, AccessFlags.Read);
                builder.SetRenderAttachment(activeColor, 0);

                builder.SetRenderFunc((CopyPassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>("ReversePhi_SaveHistory", out var passData))
            {
                passData.source = tempTexture;
                builder.UseTexture(passData.source, AccessFlags.Read);
                builder.SetRenderAttachment(historyTexture, 0);

                builder.SetRenderFunc((CopyPassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                });
            }
        }
    }

    [System.Serializable]
    public class Settings
    {
        public Shader shader;
        [Range(0f, 1f)] public float blend = 0.5f;
        public Vector2 offset = new Vector2(0.015f, 0f);
    }

    public Settings settings = new Settings();
    private ReversePhiPass pass;
    private Material material;

    public override void Create()
    {
        if (settings.shader == null) return;
        material = CoreUtils.CreateEngineMaterial(settings.shader);
        pass = new ReversePhiPass
        {
            material = this.material,
            blend = settings.blend,
            offset = settings.offset
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null || !isGlobalActive) return;
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        if (material != null) CoreUtils.Destroy(material);
        pass?.Dispose();
    }
}