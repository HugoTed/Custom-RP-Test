using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline
{
    CameraRenderer renderer = new CameraRenderer();

    bool useDynamicBatching,
        useGPUInstancing,
        useLightsPerObject,
        allowHDR;

    ShadowSettings shadowSettings;

    PostFXSettings postFXSettings;

    public CustomRenderPipeline(
        bool allowHDR,
        bool useDynamicBatching, bool useGPUInstancing,
        bool useSRPBatecher,bool useLightsPerObject,ShadowSettings shadowSettings,
        PostFXSettings postFXSettings
        )
    {
        this.allowHDR = allowHDR;
        this.postFXSettings= postFXSettings;
        this.shadowSettings = shadowSettings;
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        this.useLightsPerObject = useLightsPerObject;
        //启用SRP Batcher合批
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatecher;
        GraphicsSettings.lightsUseLinearIntensity = true;

        InitializeForEditor();
    }
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach (Camera camera in cameras)
        {
            renderer.Render(
                context, camera, allowHDR,
                useDynamicBatching, useGPUInstancing, useLightsPerObject, 
                shadowSettings,postFXSettings
                );
        }
    }
}
