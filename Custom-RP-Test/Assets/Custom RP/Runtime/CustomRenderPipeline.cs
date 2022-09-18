﻿using UnityEngine;
using UnityEngine.Rendering;

public class CustomRenderPipeline : RenderPipeline
{
    CameraRenderer renderer = new CameraRenderer();

    bool useDynamicBatching,useGPUInstancing;


    public CustomRenderPipeline(bool useDynamicBatching, bool useGPUInstancing,bool useSRPBatecher)
    {
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        //启用SRP Batcher合批
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatecher;
        GraphicsSettings.lightsUseLinearIntensity = true;

    }
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach (Camera camera in cameras)
        {
            renderer.Render(context, camera,useDynamicBatching,useGPUInstancing);
        }
    }
}
