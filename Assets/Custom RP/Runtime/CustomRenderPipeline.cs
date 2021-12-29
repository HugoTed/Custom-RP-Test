using UnityEngine;
using UnityEngine.Rendering;
public class CustomRenderPipeline : RenderPipeline
{
    CameraRenderer renderer = new CameraRenderer();

    PostFXSettings postFXSettings;
    protected override void Render(ScriptableRenderContext context,Camera[] cameras)
    {
        foreach (var camera in cameras)
        {
            renderer.Render(context,camera,postFXSettings);
        }
    }

    public CustomRenderPipeline(PostFXSettings postFXSettings)
    {
        this.postFXSettings = postFXSettings;
        //¿ªÆôSRP batching
        GraphicsSettings.useScriptableRenderPipelineBatching = true;
    }
}
