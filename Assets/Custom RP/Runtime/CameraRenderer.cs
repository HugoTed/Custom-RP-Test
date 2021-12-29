using UnityEngine;
using UnityEngine.Rendering;

//什么是局部类？
//这是一种将类或结构定义拆分为多个部分的方法，分别存储在不同的文件中，它唯一的目的就是组织代码。
//典型的用例是将自动生成的代码与手工编写的代码分开。就编译器而言，它都是同一个类定义的一部分。
public partial class CameraRenderer : MonoBehaviour
{
    ScriptableRenderContext context;
    Camera camera;

    const string bufferName = "Render Camera";
    //需要用缓冲区来绘制场景中的其他模型
    CommandBuffer buffer = new CommandBuffer() { name = bufferName};

    CullingResults cullingResults;
    //阴影pass着色器tag id
    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");

    //post fx stack
    PostFXStack postFXStack = new PostFXStack();

    //使用frame buffer获取一个渲染纹理
    static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");
    
    public void Render(ScriptableRenderContext context,Camera camera,PostFXSettings postFXSettings)
    {
        this.context = context;
        this.camera = camera;
        //绘制ui
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

    //将摄像机的属性应用于上下文
    void Setup()
    {
        //将摄像机的属性应用于上下文
        context.SetupCameraProperties(camera);
        //通过调整clear flags来结合两相机的画面
        CameraClearFlags flags = camera.clearFlags;

        //所以为了给活动堆栈提供一个源纹理，我们必须使用一个渲染纹理作为摄像机的中间帧缓冲区。
        //获取一个渲染纹理并将其设置为渲染目标，就像阴影贴图一样，
        //只不过我们将使用RenderTextureFormat.Default格式。
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

        //清除旧的内容
        
        buffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            //当flag设置成color时，我们只需要清除颜色缓冲区
            flags == CameraClearFlags.Color,
            //因为在线性空间下渲染，所以背景色要转换到线性空间
            flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);

        //使用命令缓冲区给profiler注入样本
        buffer.BeginSample(SampleName);
       
        //执行buffer
        ExecuteBuffer();
        
    }

    //向上下文发出的命令都是缓冲的，必须调用submit来提交排队的工作才能执行
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
        //排序设定
        //相机的透明度排序模式用于确定是使用正交排序还是基于距离的排序。
        var sortingSettings = new SortingSettings(camera)
        {   //使用CommonOpaque强制渲染顺序
            criteria =SortingCriteria.CommonOpaque
        };
        //绘图设置
        var drawingSettings = new DrawingSettings(unlitShaderTagId,sortingSettings);
        //过滤设置
        //先绘制不透明物体
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
        context.DrawSkybox(camera);

        //先绘制不透明物体，然后是天空盒，最后才是透明物体
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    //剔除
    //out关键字负责正确设置参数，替换值
    bool Cull()
    {
        //内联建立声明
        if(camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            //实际的cull是通过context的cull完成的，成功的话将结果储存在cullingResults中。
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }

    void Cleanup()
    {
        if (postFXStack.IsActive)
        {
            //释放TemporaryRT
            buffer.ReleaseTemporaryRT(frameBufferId);
        }
    }
   

   
}
