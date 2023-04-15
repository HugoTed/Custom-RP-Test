using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");

    const string bufferName = "Render Camera";

    CommandBuffer buffer = new CommandBuffer() { name = bufferName };

    ScriptableRenderContext context;

    Camera camera;

    CullingResults cullingResults;

    static ShaderTagId
        unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
        litShaderTagId = new ShaderTagId("CustomLit");

    Lighting lighting = new Lighting();

    PostFXStack postFXStack = new PostFXStack();

    bool useHDR;

    public void Render(ScriptableRenderContext context, Camera camera,bool allowHDR,
        bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
        ShadowSettings shadowSettings,PostFXSettings postFXSettings,
        int colorLUTResolution)
    {
        this.context = context;
        this.camera = camera;
        PrepareBuffer();
        //在剔除之前完成UI
        PrepareForSceneWindow();
        if (!Cull(shadowSettings.maxDistance))
        {
            return;
        }
        useHDR = allowHDR && camera.allowHDR;
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
        //初始化灯光
        lighting.Setup(context, cullingResults, shadowSettings, useLightsPerObject);
        postFXStack.Setup(context, camera, postFXSettings, useHDR, colorLUTResolution);
        buffer.EndSample(SampleName);
        Setup();
        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing, useLightsPerObject);
        DrawUnsupportShaders();
        DrawGizmosBeforeFX();
        if (postFXStack.isActive())
        {
            postFXStack.Render(frameBufferId);
        }
        DrawGizmosAfterFX();
        Cleanup();
        Sumbit();
    }

    void Setup()
    {
        //设置视图投影矩阵
        context.SetupCameraProperties(camera);
        //CameraClearFlags枚举定义了四个值。从 1 到 4，它们是Skybox、Color、Depth和Nothing。
        
        CameraClearFlags flags = camera.clearFlags;
        if (postFXStack.isActive())
        {
            if (flags > CameraClearFlags.Color)
            {
                flags= CameraClearFlags.Color;
            }
            buffer.GetTemporaryRT(
                frameBufferId, camera.pixelWidth, camera.pixelHeight,
                32, FilterMode.Bilinear,
                useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
                );
            //设置为渲染目标
            buffer.SetRenderTarget(
                frameBufferId,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
                );
        }
        //除了Nothing值，在flags的值不大于Depth的所有情况下都必须清除深度缓冲区(depth buffer)。
        //渲染之前清除旧内容
        buffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            flags == CameraClearFlags.Color,
            flags == CameraClearFlags.Color?
                camera.backgroundColor.linear:Color.clear
            );

        //使其出现在frame debugger中
        buffer.BeginSample(SampleName);

        ExecuteBuffer();
    }

    void Sumbit()
    {
        
        buffer.EndSample(SampleName);
        ExecuteBuffer();
        context.Submit();
    }

    void ExecuteBuffer()
    {
        //执行缓冲区
        context.ExecuteCommandBuffer(buffer);
        //执行完清除
        buffer.Clear();
    }

    void DrawVisibleGeometry(bool useDynamicBatching,bool useGPUInstancing,bool useLightsPerObject)
    {
        //是否启用lightsPerObject模式
        //启用了该模式，Unity会确定哪些灯光影响每个对象并将此信息发送到GPU
        PerObjectData lightsPerObjectFlags = useLightsPerObject ?
            PerObjectData.LightData | PerObjectData.LightIndices :
            PerObjectData.None;
        //对可见对象进行排序，以及要调用的shader pass
        var sortingSettings = new SortingSettings(camera)
        {
            //强制渲染顺序
            criteria = SortingCriteria.CommonOpaque
        };
        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings)
        {
            //启用动态批处理
            enableDynamicBatching = useDynamicBatching,
            //禁用GPU Instance
            enableInstancing = useGPUInstancing,
            //启用lightmap数据
            perObjectData =
                PerObjectData.ReflectionProbes |
                PerObjectData.Lightmaps | PerObjectData.ShadowMask |
                PerObjectData.LightProbe | PerObjectData.OcclusionProbe |
                PerObjectData.LightProbeProxyVolume |
                PerObjectData.OcclusionProbeProxyVolume |
                lightsPerObjectFlags
        };
        //设置lit pass
        drawingSettings.SetShaderPassName(1, litShaderTagId);
        //允许所有渲染队列
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
        //调用剔除结果作为参数进行渲染
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
        context.DrawSkybox(camera);

        //先渲染不透明物体，再渲染天空盒，再由远到近渲染透明物体
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    bool Cull(float maxShadowDistance)
    {
        //尝试剔除
        if(camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            //是否成功检索参数
            p.shadowDistance = Mathf.Min(maxShadowDistance,camera.farClipPlane);
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }

    void Cleanup()
    {
        lighting.Cleanup();
        if (postFXStack.isActive())
        {
            buffer.ReleaseTemporaryRT(frameBufferId);
        }
    }
}
