using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName ="Rendering/Custom Render Pineline")]
public class CustomRenderPineLineAsset : RenderPipelineAsset
{
    [SerializeField]
    PostFXSettings postFXSettings = default;
    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(postFXSettings);
    }
}
