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

    const int maxShadowedDirectionalLightCount = 4, maxShadowedOtherLightCount = 16;
    const int maxCascades = 4;

    Vector4 atlasSizes;

    static int
        dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
        dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
        otherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas"),
        otherShadowMatricesId = Shader.PropertyToID("_OtherShadowMatrices"),
        cascadeCountId = Shader.PropertyToID("_CascadeCount"),
        cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
        cascadeDataId = Shader.PropertyToID("_CascadeData"),
        shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
        shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade"),
        shadowPancakingId = Shader.PropertyToID("_ShadowPancaking");

    static string[] cascadeBlendKeywords =
    {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };

    static Vector4[]
        cascadeCullingSpheres = new Vector4[maxCascades],
        cascadeData = new Vector4[maxCascades];

    //阴影变换矩阵
    static Matrix4x4[]
        dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades],
        otherShadowMatrices = new Matrix4x4[maxShadowedOtherLightCount];

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

    int shadowedDirectionalLightCount, shadowedOtherLightCount;

    bool useShadowMask;
    static string[] shadowMaskKeywords =
    {
        "_SHADOW_MASK_ALWAYS",
        "_SHADOW_MASK_DISTANCE"
    };

    //其他光源PCF变体
    static string[] otherFilterKeywords = {
        "_OTHER_PCF3",
        "_OTHER_PCF5",
        "_OTHER_PCF7",
    };

    struct ShadowedOtherLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float normalBias;
    }

    ShadowedOtherLight[] shadowedOtherLights =
        new ShadowedOtherLight[maxShadowedOtherLightCount];

    public void Setup(ScriptableRenderContext context,CullingResults cullingResults,ShadowSettings shadowSettings)
    {
        this.context = context;
        this.cullingResults = cullingResults;
        this.settings = shadowSettings;
        shadowedDirectionalLightCount = 0;
        shadowedOtherLightCount = 0;
        useShadowMask = false;
    }
    //阴影贴图中为灯光的阴影贴图保留空间，并存储渲染它们所需的信息
    public Vector4 ReserveDirectionalShadows(Light light,int visibleLightIndex)
    {
        if (shadowedDirectionalLightCount < maxShadowedDirectionalLightCount &&
            light.shadows != LightShadows.None && light.shadowStrength > 0f)
        {
            //shadow mask的通道
            float maskChannel = -1;
            //使用烘焙的shadow mask
            LightBakingOutput lightBaking = light.bakingOutput;
            if(
                lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
                )
            {
                useShadowMask = true;
                maskChannel = lightBaking.occlusionMaskChannel;
            }
            //首先确定光线是否使用阴影掩模。
            //之后检查是否没有实时阴影投射器，在这种情况下只有阴影强度是相关的。
            if (!cullingResults.GetShadowCasterBounds(
                visibleLightIndex,out Bounds b
                ))
            {
                //当阴影强度大于0时，着色器会对阴影贴图采样
                //但那是不正确的，因此在shadowStrength前取负号
                //在GetBakedShadow中取abs
                return new Vector4(-light.shadowStrength, 0f, 0f,maskChannel);
            }
            //灯光没有开启阴影或者阴影强度为0就忽略这个灯光
            shadowedDirectionalLights[shadowedDirectionalLightCount] =
                new ShadowedDirectionalLight
                {
                    visibleLightIndex = visibleLightIndex,
                    slopeScaleBias = light.shadowBias,
                    nearPlaneOffset = light.shadowNearPlane
                };
            //返回阴影强度和阴影贴图偏移索引,阴影法线偏移
            return new Vector4(
                light.shadowStrength, 
                settings.directional.cascadeCount * shadowedDirectionalLightCount++,
                light.shadowNormalBias,maskChannel
                );
        }
        return Vector4.zero;
    }

    public void Render()
    {
        if(shadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {//如果没有阴影，也要声明一张shadow map后面cleanup才可以释放
            buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1,
                32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }
        if (shadowedOtherLightCount>0)
        {
            RenderOtherShadows();
        }
        else
        {
            buffer.SetGlobalTexture(otherShadowAtlasId,dirShadowAtlasId);
        }

        buffer.BeginSample(bufferName);
        SetKeywords(shadowMaskKeywords, useShadowMask ?
            QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 :
            -1);

        buffer.SetGlobalInt(
            cascadeCountId,
            shadowedDirectionalLightCount > 0 ? settings.directional.cascadeCount : 0);
        float f = 1f - settings.directional.cascadeFade;
        buffer.SetGlobalVector(shadowDistanceFadeId,
            new Vector4(1f / settings.maxDistance, 1f / settings.distanceFade,
                        1f / (1f - f * f)));

        buffer.SetGlobalVector(shadowAtlasSizeId, atlasSizes);
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    void RenderDirectionalShadows()
    {
        int atlasSize = (int)settings.directional.atlasSize;
        atlasSizes.x = atlasSize;
        atlasSizes.y = 1f / atlasSize;
        buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize,
            32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        buffer.SetRenderTarget(
            dirShadowAtlasId,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.SetGlobalFloat(shadowPancakingId, 1f);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();
        //如果有多个灯光，就分割图集,级联阴影
        int tiles = shadowedDirectionalLightCount * settings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        for (int i = 0; i < shadowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i, split, tileSize);
        }
        
        buffer.SetGlobalVectorArray(
            cascadeCullingSpheresId, cascadeCullingSpheres);
        buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
       
        SetKeywords(directionalFilterKeywords,(int)settings.directional.filter - 1);
        SetKeywords(cascadeBlendKeywords, (int)settings.directional.cascadeBlend - 1);
        //图集大小，纹素大小
        //buffer.SetGlobalVector(
        //    shadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize)
        //);
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    private void SetKeywords(string[] keywords,int enabledIndex)
    {
        for (int i = 0; i < keywords.Length; i++)
        {
            if (i == enabledIndex)
            {
                buffer.EnableShaderKeyword(keywords[i]);
            }
            else
            {
                buffer.DisableShaderKeyword(keywords[i]);
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

        float cullingFactor =
            Mathf.Max(0f, 0.8f - settings.directional.cascadeFade);
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
            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
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

    public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex)
    {
        if (light.shadows == LightShadows.None && light.shadowStrength <= 0f)
        {
            return new Vector4(0f, 0f, 0f, -1f);
        }

        float maskChannel = -1f;
        LightBakingOutput lightBaking = light.bakingOutput;
        if (
            lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
            lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
            )
        {
            useShadowMask = true;
            maskChannel = lightBaking.occlusionMaskChannel;
        }
        //返回之前检查灯光是否超过最大值
        //检查是否有阴影可以渲染
        if (
            shadowedOtherLightCount >= maxShadowedOtherLightCount ||
            !cullingResults.GetShadowCasterBounds(visibleLightIndex,out Bounds b)
            )
        {
            return new Vector4(-light.shadowStrength, 0f, 0f,maskChannel);
        }

        shadowedOtherLights[shadowedOtherLightCount] = new ShadowedOtherLight
        {
            visibleLightIndex = visibleLightIndex,
            slopeScaleBias = light.shadowBias,
            normalBias = light.shadowNormalBias
        };

        return new Vector4(
            light.shadowStrength, shadowedOtherLightCount++, 0f,
            maskChannel
        );
    }

    void RenderOtherShadows()
    {
        int atlasSize = (int)settings.other.atlasSize;
        atlasSizes.z = atlasSize;
        atlasSizes.w = 1f / atlasSize;
        buffer.GetTemporaryRT(otherShadowAtlasId, atlasSize, atlasSize,
            32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        buffer.SetRenderTarget(
            otherShadowAtlasId,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.SetGlobalFloat(shadowPancakingId, 0f);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();
        //如果有多个灯光，就分割图集
        int tiles = shadowedOtherLightCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        for (int i = 0; i < shadowedOtherLightCount; i++)
        {
            RenderSpotShadows(i, split, tileSize);
        }
       
        buffer.SetGlobalMatrixArray(otherShadowMatricesId, otherShadowMatrices);

        SetKeywords(otherFilterKeywords, (int)settings.other.filter - 1);

        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    //它与带参数的 RenderDirectionalShadows 方法的作用相同，
    //只是它不循环多个图块，没有级联，也没有剔除因子。
    void RenderSpotShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadowSettings =
            new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(
            light.visibleLightIndex, out Matrix4x4 viewMatrix,
            out Matrix4x4 projectionMatrix, out ShadowSplitData splitData
        );
        shadowSettings.splitData = splitData;
        otherShadowMatrices[index] = ConvertToAtlasMatrix(
            projectionMatrix * viewMatrix,
            SetTileViewport(index, split, tileSize), split
        );
        buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
        buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
        ExecuteBuffer();
        context.DrawShadows(ref shadowSettings);
        buffer.SetGlobalDepthBias(0f, 0f);
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
        if(shadowedOtherLightCount > 0)
        {
            buffer.ReleaseTemporaryRT(otherShadowAtlasId);
        }
        ExecuteBuffer();
    }
}
