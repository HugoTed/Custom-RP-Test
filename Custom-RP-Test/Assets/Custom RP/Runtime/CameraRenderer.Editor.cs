using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEngine.Profiling;

partial class CameraRenderer
{
    string SampleName { get; set; }
    partial void DrawGizmosBeforeFX();

    partial void DrawGizmosAfterFX();

    partial void DrawUnsupportShaders();

    partial void PrepareForSceneWindow();

    partial void PrepareBuffer();
#if UNITY_EDITOR

    static Material errorMaterial;

    //旧版的着色器就算不支持了也要渲染出来
    static ShaderTagId[] legacyShaderTagIds = new ShaderTagId[]
    {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM"),
    };

    //渲染Gizmos
    partial void DrawGizmosBeforeFX()
    {
        if (Handles.ShouldRenderGizmos())
        {
            if (useIntermediateBuffer)
            {
                Draw(depthAttachmentId, BuiltinRenderTextureType.CameraTarget, true);
                ExecuteBuffer();
            }
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
        }
    }

    partial void DrawGizmosAfterFX()
    {
        if (Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
    }

    //渲染不支持的shader
    partial void DrawUnsupportShaders()
    {
        if(errorMaterial == null)
        {
            errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }
        var drawingSettings = new DrawingSettings(legacyShaderTagIds[0], new SortingSettings(camera))
        {
            overrideMaterial = errorMaterial
        };

        for (int i = 1; i < legacyShaderTagIds.Length; i++)
        {
            drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
        }
        var filteringSettings = FilteringSettings.defaultValue;

        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    //编辑器场景中渲染ui
    partial void PrepareForSceneWindow()
    {
        if(camera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        }
    }
    //给不同摄像机的buffer定义不同的名称
    partial void PrepareBuffer()
    {
        Profiler.BeginSample("Editor Only");
        buffer.name = SampleName = camera.name;
        Profiler.EndSample();
    }
#else
    string SampleName => bufferName;
#endif
}
