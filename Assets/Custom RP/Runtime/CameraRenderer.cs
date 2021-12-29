using UnityEngine;
using UnityEngine.Rendering;

//ʲô�Ǿֲ��ࣿ
//����һ�ֽ����ṹ������Ϊ������ֵķ������ֱ�洢�ڲ�ͬ���ļ��У���Ψһ��Ŀ�ľ�����֯���롣
//���͵������ǽ��Զ����ɵĴ������ֹ���д�Ĵ���ֿ����ͱ��������ԣ�������ͬһ���ඨ���һ���֡�
public partial class CameraRenderer : MonoBehaviour
{
    ScriptableRenderContext context;
    Camera camera;

    const string bufferName = "Render Camera";
    //��Ҫ�û����������Ƴ����е�����ģ��
    CommandBuffer buffer = new CommandBuffer() { name = bufferName};

    CullingResults cullingResults;
    //��Ӱpass��ɫ��tag id
    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");

    //post fx stack
    PostFXStack postFXStack = new PostFXStack();

    //ʹ��frame buffer��ȡһ����Ⱦ����
    static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");
    
    public void Render(ScriptableRenderContext context,Camera camera,PostFXSettings postFXSettings)
    {
        this.context = context;
        this.camera = camera;
        //����ui
        PrepareForSceneWindow();
        PrepareBuffer();
        if (!Cull())
        {
            return;
        }

        buffer.BeginSample(SampleName);
        ExecuteBuffer();

        postFXStack.Setup(context, camera,postFXSettings);

        buffer.EndSample(SampleName);

        Setup();
        DrawVisableGeometry();
        DrawUnsupportedShaders();
        DrawGizmosBeforeFX();

        if (postFXStack.IsActive)
        {
            postFXStack.Render(frameBufferId);
        }
        DrawGizmosAfterFX();
        Cleanup();
        Submit();
    }

    //�������������Ӧ����������
    void Setup()
    {
        //�������������Ӧ����������
        context.SetupCameraProperties(camera);
        //ͨ������clear flags�����������Ļ���
        CameraClearFlags flags = camera.clearFlags;

        //����Ϊ�˸����ջ�ṩһ��Դ�������Ǳ���ʹ��һ����Ⱦ������Ϊ��������м�֡��������
        //��ȡһ����Ⱦ������������Ϊ��ȾĿ�꣬������Ӱ��ͼһ����
        //ֻ�������ǽ�ʹ��RenderTextureFormat.Default��ʽ��
        if (postFXStack.IsActive)
        {
            if (flags > CameraClearFlags.Color)
            {
                flags = CameraClearFlags.Color;
            }
            buffer.GetTemporaryRT(frameBufferId,
                camera.pixelWidth, camera.pixelHeight,
                32,
                FilterMode.Bilinear,
                RenderTextureFormat.Default);
            buffer.SetRenderTarget(frameBufferId,
                RenderBufferLoadAction.DontCare,RenderBufferStoreAction.Store);
        }

        //����ɵ�����
        
        buffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            //��flag���ó�colorʱ������ֻ��Ҫ�����ɫ������
            flags == CameraClearFlags.Color,
            //��Ϊ�����Կռ�����Ⱦ�����Ա���ɫҪת�������Կռ�
            flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);

        //ʹ�����������profilerע������
        buffer.BeginSample(SampleName);
       
        //ִ��buffer
        ExecuteBuffer();
        
    }

    //�������ķ���������ǻ���ģ��������submit���ύ�ŶӵĹ�������ִ��
    void Submit()
    {
        buffer.EndSample(SampleName);
        ExecuteBuffer();
        context.Submit();
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
    void DrawVisableGeometry()
    {
        //�����趨
        //�����͸��������ģʽ����ȷ����ʹ�����������ǻ��ھ��������
        var sortingSettings = new SortingSettings(camera)
        {   //ʹ��CommonOpaqueǿ����Ⱦ˳��
            criteria =SortingCriteria.CommonOpaque
        };
        //��ͼ����
        var drawingSettings = new DrawingSettings(unlitShaderTagId,sortingSettings);
        //��������
        //�Ȼ��Ʋ�͸������
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
        context.DrawSkybox(camera);

        //�Ȼ��Ʋ�͸�����壬Ȼ������պУ�������͸������
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    //�޳�
    //out�ؼ��ָ�����ȷ���ò������滻ֵ
    bool Cull()
    {
        //������������
        if(camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            //ʵ�ʵ�cull��ͨ��context��cull��ɵģ��ɹ��Ļ������������cullingResults�С�
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }

    void Cleanup()
    {
        if (postFXStack.IsActive)
        {
            //�ͷ�TemporaryRT
            buffer.ReleaseTemporaryRT(frameBufferId);
        }
    }
   

   
}
