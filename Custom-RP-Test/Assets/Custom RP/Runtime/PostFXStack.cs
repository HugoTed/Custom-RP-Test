using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public partial class PostFXStack
{
    int
        bloomBicubicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling"),
        bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter"),
        bloomThresholdId = Shader.PropertyToID("_BloomThreshold"),
        bloomIntensityId = Shader.PropertyToID("_BloomIntensity"),
        fxSourceId = Shader.PropertyToID("_PostFXSource"),
        fxSource2Id = Shader.PropertyToID("_PostFXSource2");

    const string bufferName = "Post FX";

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName,
    };

    ScriptableRenderContext context;

    Camera camera;

    PostFXSettings settings;

    enum Pass
    {
        BloomHorizontal,
        BloomVertical,
        BloomCombine,
        BloomPrefilter,
        Copy
    }

    const int maxBloomPyramidLevels = 16;
    int bloomPyramidId;

    public PostFXStack()
    {
        bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
        for (int i = 1; i < maxBloomPyramidLevels * 2; i++)
        {
            Shader.PropertyToID("_BloomPyramid" + i);
        }
    }

    public void Setup(
        ScriptableRenderContext context, Camera camera,PostFXSettings settings)
    {
        this.context = context;
        this.camera = camera;
        this.settings =
            camera.cameraType <= CameraType.SceneView ? settings : null;
        ApplySceneViewState();
    }

    public bool isActive() => settings != null;

    public void Render(int sourceId)
    {
        DoBloom(sourceId);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void Draw(
        RenderTargetIdentifier from,RenderTargetIdentifier to,Pass pass
        )
    {
        buffer.SetGlobalTexture(fxSourceId, from);
        buffer.SetRenderTarget(
            to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
        buffer.DrawProcedural(
            Matrix4x4.identity, settings.Material, (int)pass,
            MeshTopology.Triangles, 3
            );
    }

    void DoBloom(int sourceId)
    {
        buffer.BeginSample("Bloom");
        PostFXSettings.BloomSettings bloom = settings.Bloom;
        int width = camera.pixelWidth/2,height = camera.pixelHeight/2;
        if (
            bloom.maxIterations == 0 || bloom.intensity <= 0f ||
            height < bloom.downScaleLimit * 2 || width < bloom.downScaleLimit * 2
            )
        {
            Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
            buffer.EndSample("Bloom");
            return;
        }

        Vector4 threshold;
        threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
        threshold.y = threshold.x * bloom.thresholdKnee;
        threshold.z = 2f * threshold.y;
        threshold.w = 0.25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;
        buffer.SetGlobalVector(bloomThresholdId, threshold);

        RenderTextureFormat format = RenderTextureFormat.Default;
        buffer.GetTemporaryRT(
            bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format
            );
        Draw(sourceId, bloomPrefilterId, Pass.BloomPrefilter);
        width /= 2; 
        height/=2;

        int fromId = bloomPrefilterId, toId = bloomPyramidId + 1;
        int i;
        //1           ½    ¼   ⅛     1
        //sourceId → 0
        //            ↓
        //            1 → 2
        //                 ↓
        //                 3 → 4
        //                      ↓
        //                      5 → Dest       
        for (i = 0; i < bloom.maxIterations; i++)
        {
            if (height < bloom.downScaleLimit || width < bloom.downScaleLimit)
            {
                break;
            }
            int midId = toId - 1;
            buffer.GetTemporaryRT(
                midId, width, height, 0, FilterMode.Bilinear, format
                );
            buffer.GetTemporaryRT(
                toId, width, height, 0, FilterMode.Bilinear, format
                );
            Draw(fromId, midId, Pass.BloomHorizontal);
            Draw(midId, toId, Pass.BloomVertical);
            fromId = toId;
            toId += 2;
            width/=2; 
            height /= 2;
        }

        buffer.ReleaseTemporaryRT(bloomPrefilterId);
        //1           ½    ¼   ⅛
        //sourceId → 0
        //            ↓
        //            1 → 2
        //            |    ↓
        //            |    3 → 4
        //            ↓   ↓   ↓
        // Dest    ← 0 ← 2 ← 5     
        //
        buffer.SetGlobalFloat(
            bloomBicubicUpsamplingId, bloom.bicubicUpsampling ? 1f : 0f
            );
        buffer.SetGlobalFloat(bloomIntensityId, 1f);
        if(i > 1)
        {
            buffer.ReleaseTemporaryRT(fromId - 1);
            toId -= 5;

            for (i -= 1; i > 0; i--)
            {
                buffer.SetGlobalTexture(fxSource2Id, toId + 1);
                Draw(fromId, toId, Pass.BloomCombine);
                buffer.ReleaseTemporaryRT(fromId);
                buffer.ReleaseTemporaryRT(toId + 1);
                fromId = toId;
                toId -= 2;
            }
        }
        else
        {
            buffer.ReleaseTemporaryRT(bloomPyramidId);
        }
        buffer.SetGlobalFloat(bloomIntensityId, bloom.intensity);
        buffer.SetGlobalTexture(fxSource2Id, sourceId);
        Draw(fromId,BuiltinRenderTextureType.CameraTarget,Pass.BloomCombine);
        buffer.ReleaseTemporaryRT(fromId);
        buffer.EndSample("Bloom");
    }
}
