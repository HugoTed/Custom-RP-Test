using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEngine.Profiling;

partial class CameraRenderer : MonoBehaviour
{
    //声明局部方法，release版本中不包含这部分代码

    string SampleName { get; set; }

    //绘制gizmos,在fx效果前后绘制是不一样的，所以分开两个方法
    partial void DrawGizmosBeforeFX();

    partial void DrawGizmosAfterFX();
    partial void DrawUnsupportedShaders();

    //在scene view中绘制ui
    partial void PrepareForSceneWindow();

    //每个buffer都设置成摄像机的名字
    partial void PrepareBuffer();
    //只作用在编辑器中
#if UNITY_EDITOR
    static ShaderTagId[] legacyShaderTagIds =
    {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };

    //定义一个错误shader显示用的material，用于替换
    static Material errorMaterial;
    

    //绘制不支持的shader
    partial void DrawUnsupportedShaders()
    {
        if(errorMaterial == null)
        {
            errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }
        var drawingSettings = new DrawingSettings(legacyShaderTagIds[0],new SortingSettings(camera))
        {
            //覆盖错误材质
            overrideMaterial = errorMaterial,
        };
        for (int i = 0; i < legacyShaderTagIds.Length; i++)
        {
            drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
        }
        var filteringSettings = FilteringSettings.defaultValue;
        context.DrawRenderers(cullingResults,ref drawingSettings,ref filteringSettings);
    }

    partial void DrawGizmosBeforeFX()
    {
        if (Handles.ShouldRenderGizmos())
        {
            //绘制gizmos,两个子集，一个图像处理之前的，一个之后的，这里两个都用
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            //context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
    }

    partial void DrawGizmosAfterFX()
    {
        if (Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
    }

    partial void PrepareForSceneWindow()
    {
        //如果camera类型是Scene view就绘制编辑ui
        if (camera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        }
    }

    partial void PrepareBuffer()
    {
        Profiler.BeginSample("Editor Only");
        buffer.name = SampleName = camera.name;
        Profiler.EndSample();
    }

#else

    const string SampleName = bufferName;

#endif



}
