using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEngine.Profiling;

partial class CameraRenderer : MonoBehaviour
{
    //�����ֲ�������release�汾�в������ⲿ�ִ���

    string SampleName { get; set; }

    //����gizmos,��fxЧ��ǰ������ǲ�һ���ģ����Էֿ���������
    partial void DrawGizmosBeforeFX();

    partial void DrawGizmosAfterFX();
    partial void DrawUnsupportedShaders();

    //��scene view�л���ui
    partial void PrepareForSceneWindow();

    //ÿ��buffer�����ó������������
    partial void PrepareBuffer();
    //ֻ�����ڱ༭����
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

    //����һ������shader��ʾ�õ�material�������滻
    static Material errorMaterial;
    

    //���Ʋ�֧�ֵ�shader
    partial void DrawUnsupportedShaders()
    {
        if(errorMaterial == null)
        {
            errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }
        var drawingSettings = new DrawingSettings(legacyShaderTagIds[0],new SortingSettings(camera))
        {
            //���Ǵ������
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
            //����gizmos,�����Ӽ���һ��ͼ����֮ǰ�ģ�һ��֮��ģ�������������
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
        //���camera������Scene view�ͻ��Ʊ༭ui
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
