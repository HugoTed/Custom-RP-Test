using System;
using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
    const string bufferName = "Shadows";

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    ScriptableRenderContext context;

    CullingResults cullingResults;

    ShadowSettings settings;

    const int maxShadowedDirectionalLightCount = 4, maxCascades = 4;

    static int
        dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
        dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
        cascadeCountId = Shader.PropertyToID("_CascadeCount"),
        cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
        cascadeDataId = Shader.PropertyToID("_CascadeData"),
        shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
        shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");


    static Vector4[]
        cascadeCullingSpheres = new Vector4[maxCascades],
        cascadeData = new Vector4[maxCascades];

    //阴影变换矩阵
    static Matrix4x4[]
        dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades];

    //PCF变体
    static string[] directionalFilterKeywords =
    {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7",
    };

    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }

    ShadowedDirectionalLight[] shadowedDirectionalLights =
        new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];

    int ShadowedDirectionalLightCount;
    public void Setup(ScriptableRenderContext context,CullingResults cullingResults,ShadowSettings shadowSettings)
    {
        this.context = context;
        this.cullingResults = cullingResults;
        this.settings = shadowSettings;
        ShadowedDirectionalLightCount = 0;
    }
    //阴影贴图中为灯光的阴影贴图保留空间，并存储渲染它们所需的信息
    public Vector3 ReserveDirectionalShadows(Light light,int visibleLightIndex)
    {
        if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount &&
            light.shadows != LightShadows.None && light.shadowStrength > 0f &&
            cullingResults.GetShadowCasterBounds(visibleLightIndex,out Bounds b))
        {//灯光没有开启阴影或者阴影强度为0就忽略这个灯光
            shadowedDirectionalLights[ShadowedDirectionalLightCount] =
                new ShadowedDirectionalLight
                {
                    visibleLightIndex = visibleLightIndex,
                    slopeScaleBias = light.shadowBias,
                    nearPlaneOffset = light.shadowNearPlane
                };
            //返回阴影强度和阴影贴图偏移索引,阴影法线偏移
            return new Vector3(
                light.shadowStrength, 
                settings.directional.cascadeCount * ShadowedDirectionalLightCount++,
                light.shadowNormalBias
                );
        }
        return Vector3.zero;
    }

    public void Render()
    {
        if(ShadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {//如果没有阴影，也要声明一张shadow map后面cleanup才可以释放
            buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1,
                32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }
    }

    void RenderDirectionalShadows()
    {
        int atlasSize = (int)settings.directional.atlasSize;
        buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize,
            32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        buffer.SetRenderTarget(
            dirShadowAtlasId,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();
        //如果有多个灯光，就分割图集,级联阴影
        int tiles = ShadowedDirectionalLightCount * settings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        for (int i = 0; i < ShadowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i, split, tileSize);
        }
        buffer.SetGlobalInt(cascadeCountId, settings.directional.cascadeCount);
        buffer.SetGlobalVectorArray(
            cascadeCullingSpheresId, cascadeCullingSpheres);
        buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
        float f = 1f - settings.directional.cascadeFade;
        buffer.SetGlobalVector(shadowDistanceFadeId,
            new Vector4(1f / settings.maxDistance, 1f / settings.distanceFade,
                        1f / (1f - f * f)));
        SetKeywords();
        //图集大小，纹素大小
        buffer.SetGlobalVector(
            shadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize)
        );
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    private void SetKeywords()
    {
        int enabledIndex = (int)settings.directional.filter - 1;
        for (int i = 0; i < directionalFilterKeywords.Length; i++)
        {
            if (i == enabledIndex)
            {
                buffer.EnableShaderKeyword(directionalFilterKeywords[i]);
            }
            else
            {
                buffer.DisableShaderKeyword(directionalFilterKeywords[i]);
            }
        }
    }

    void RenderDirectionalShadows(int index,int split, int tileSize)
    {
        ShadowedDirectionalLight light = shadowedDirectionalLights[index];
        var shadowSettings =
            new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        //级联阴影
        int cascadeCount = settings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = settings.directional.CascadeRatios;

        for (int i = 0; i < cascadeCount; i++)
        {
            //计算灯光与摄像机可见区域投影矩阵
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, i, cascadeCount, ratios, tileSize,
                light.nearPlaneOffset, out Matrix4x4 viewMatrix,
                out Matrix4x4 projectionMatrix, out ShadowSplitData splitData
                );
            //拆分数据包含有关如何剔除阴影投射对象的信息，
            //我们必须将其复制到阴影设置中。
            shadowSettings.splitData = splitData;
            //保持级联剔除球体的分割数据
            //因为每盏灯都是等价的，所以只对第一盏灯这样做
            if(index == 0)
            {
                SetCascadeData(i, splitData.cullingSphere, tileSize);
            }
            int tileIndex = tileOffset + i;
            //世界空间->光照空间
            dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
                projectionMatrix * viewMatrix,
                SetTileViewport(tileIndex, split, tileSize), split
                );
            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0f, 0f);
        }
        
    }

    void SetCascadeData(int index,Vector4 cullingSphere,float tileSize)
    {
        //cullingSphere.w为半径
        float texelSize = 2f * cullingSphere.w / tileSize;
        //PCF
        float filterSize = texelSize * ((float)settings.directional.filter + 1f);       
        cascadeData[index] = new Vector4(
            1.0f / cullingSphere.w,
            //因为纹素是正方形,缩放√2
            filterSize * 1.4142136f
            );
        cullingSphere.w -= filterSize;
        cullingSphere.w *= cullingSphere.w;

        cascadeCullingSpheres[index] = cullingSphere;
    }

    Vector2 SetTileViewport(int index,int split,float tileSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);
        buffer.SetViewport(new Rect(
            offset.x * tileSize, offset.y * tileSize, tileSize, tileSize
            ));
        return offset;
    }
    //返回一个从世界空间转换为阴影瓦片空间的矩阵
    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m,Vector2 offset,int split)
    {
        //如果是反向z深度，就取相反值
        //opengl里面1表示最大深度，但其他API可能不是
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }
        
        float scale = 1f / split;//应用瓦片偏移和缩放
        //↑ 1                 1↑   |          
        //|                     |    |
        //|                 0.5 |----十----
        //|__________→     ->  |____|_____→   
        //0            1        0    0.5      1
        // ↙ = 0.5 * ScreenUV
        // ↘ = 0.5 * ScreenUV + (0.5, 0)
        // ↖ = 0.5 * ScreenUV + (0, 0.5)
        // ↗ = 0.5 * ScreenUV + (0.5, 0.5)
        // ScreenUV = mul(VP,worldPos)
        //裁剪空间范围[-1,1],中心为0，
        //但是纹理坐标范围[0,1]
        // ↙ = 0.5 * (ScreenUV * 0.5 + 0.5)
        // ↘ = 0.5 * (ScreenUV * 0.5 + 0.5) + (0.5, 0)
        //...

        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);
        return m;
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public void Cleanup()
    {
        //shadow map用完就要释放
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        ExecuteBuffer();
    }
}
