using UnityEngine;
using UnityEngine.Rendering;

public class PostFXStack
{
    const string bufferName = "Post FX";

    CommandBuffer buffer = new CommandBuffer()
    {
        name = bufferName
    };

    ScriptableRenderContext context;

    Camera camera;

    PostFXSettings settings;

    enum Pass
    {
        Copy
    }

    int fxSourceId = Shader.PropertyToID("_PostFXSource");

    public void Setup(ScriptableRenderContext context,Camera camera,PostFXSettings settings)
    {
        this.context = context;
        this.camera = camera;
        this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
    }

    public bool IsActive => settings!=null;

    public void Render(int sourceId)
    {
        //������������ǲ���Ҫ����Begin/EndSample,��Ϊ����û��ʹ��ClearRenderTarget��������ȫ���ǻ���
        //buffer.Blit(sourceId, BuiltinRenderTextureType.CameraTarget);
        Draw(sourceId,BuiltinRenderTextureType.CameraTarget,Pass.Copy);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    //����һ��������ȡ��ԭ������Ļ����Ч�������Σ����������ֻ����������
    void Draw(RenderTargetIdentifier from,RenderTargetIdentifier to,Pass pass)
    {
        buffer.SetGlobalTexture(fxSourceId,from);
        buffer.SetRenderTarget(to,RenderBufferLoadAction.DontCare,RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int)pass, MeshTopology.Triangles, 3);
    }
}
