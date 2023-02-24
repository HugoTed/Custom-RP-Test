using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{
    const string bufferName = "Lighting";

    const int maxDirLightCount = 4, maxOtherLightCount = 4;

    //Direction Light
    static int 
        dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
        dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
        dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections"),
        dirLightShadowDataId =
            Shader.PropertyToID("_DirectionalLightShadowData");

    static Vector4[]
        dirLightColors = new Vector4[maxDirLightCount],
        dirLightDirections = new Vector4[maxDirLightCount],
        dirLightShadowData = new Vector4[maxDirLightCount];

    //Other Light
    static int
        otherLightCountId = Shader.PropertyToID("_OtherLightCount"),
        otherLightColorsId = Shader.PropertyToID("_OtherLightColors"),
        otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions"),
        otherLightDirectionsId = Shader.PropertyToID("_OtherLightDirections"),
        otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles"),
        otherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");

    static Vector4[]
        otherLightColors = new Vector4[maxOtherLightCount],
        otherLightPositions = new Vector4[maxOtherLightCount],
        otherLightDirections = new Vector4[maxOtherLightCount],
        otherLightSpotAngles = new Vector4[maxOtherLightCount],
        otherLightShadowData = new Vector4[maxOtherLightCount];


    static string lightsPerObjectKeyword = "_LIGHTS_PER_OBJECT";

    CommandBuffer buffer = new CommandBuffer()
    {
        name = bufferName
    };
    //剔除不可见光
    CullingResults cullingResults;

    Shadows shadows = new Shadows();

    public void Setup(
        ScriptableRenderContext context,CullingResults cullingResults,
        ShadowSettings shadowSettings,bool useLightsPerObject
        )
    {
        this.cullingResults = cullingResults;
        buffer.BeginSample(bufferName);
        //SetupDirectionalLight();
        //阴影
        shadows.Setup(context, cullingResults, shadowSettings);
        SetupLights(useLightsPerObject);
        shadows.Render();
        buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void SetupLights(bool useLightsPerObject)
    {
        //NativeArray是一个类似于数组的结构，但提供与本机内存缓冲区的连接。
        //它使在托管 C# 代码和本机 Unity 引擎代码之间有效共享数据成为可能。

        //Allocator.Temp获取可见光索引和场景中的其他灯光
        //当我们启用useLightsPerObject会索引灯光
        NativeArray<int> indexMap = useLightsPerObject ?
            cullingResults.GetLightIndexMap(Allocator.Temp) : default;

        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
        int dirLightCount = 0, otherLightCount = 0;
        int i;
        for (i = 0; i < visibleLights.Length; i++)
        {
            int newIndex = -1;
            VisibleLight visibleLight = visibleLights[i];

            //既支持方向光也支持点光源
            //到达最大方向光数量后，跳过多余的方向光，循环其他光源
            switch (visibleLight.lightType)
            {
                case LightType.Directional:
                    if (dirLightCount < maxDirLightCount)
                    {
                        SetupDirectionalLight(dirLightCount++, ref visibleLight);
                    }
                    break;
                case LightType.Point:
                    if (otherLightCount < maxOtherLightCount)
                    {
                        newIndex = otherLightCount;
                        SetupPointLight(otherLightCount++, ref visibleLight);
                    }
                    break;
                case LightType.Spot:
                    if(otherLightCount < maxOtherLightCount)
                    {
                        newIndex = otherLightCount;
                        SetupSpotLight(otherLightCount++,ref visibleLight);
                    }
                    break;
            }
            if (useLightsPerObject)
            {
                indexMap[i] = newIndex;
            }
        }
        if (useLightsPerObject)
        {
            //跳过不可见光的索引
            for (; i < indexMap.Length; i++)
            {
                indexMap[i] = -1;
            }
            cullingResults.SetLightIndexMap(indexMap);
            indexMap.Dispose();
            Shader.EnableKeyword(lightsPerObjectKeyword);
        }
        else
        {
            Shader.DisableKeyword(lightsPerObjectKeyword);
        }
        buffer.SetGlobalInt(dirLightCountId, dirLightCount);
        if(dirLightCount > 0)
        {
            buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
            buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
            buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
        }
        //方向光和其他光源都可以独立发送gpu
        buffer.SetGlobalInt(otherLightCountId, otherLightCount);
        if (otherLightCount > 0)
        {
            buffer.SetGlobalVectorArray(otherLightColorsId, otherLightColors);
            buffer.SetGlobalVectorArray(otherLightPositionsId, otherLightPositions);
            buffer.SetGlobalVectorArray(otherLightDirectionsId, otherLightDirections);
            buffer.SetGlobalVectorArray(otherLightSpotAnglesId, otherLightSpotAngles);
            buffer.SetGlobalVectorArray(otherLightShadowDataId, otherLightShadowData);
        }


    }

    void SetupDirectionalLight(int index,ref VisibleLight visibleLight) 
    {
        dirLightColors[index] = visibleLight.finalColor;
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        dirLightShadowData[index] = shadows.ReserveDirectionalShadows(visibleLight.light, index);
    }

    void SetupPointLight(int index, ref VisibleLight visibleLight)
    {
        otherLightColors[index] = visibleLight.finalColor;
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        //点光源的超出光照范围position平滑地过渡为0
        //避免远处镜面反射还能反射点光源
        position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightPositions[index] = position;
        otherLightSpotAngles[index] = new Vector4(0f, 1f);

        //阴影数据
        Light light = visibleLight.light;
        otherLightShadowData[index] = shadows.ReserveOtherShadows(light, index);
    }

    void SetupSpotLight(int index, ref VisibleLight visibleLight)
    {
        otherLightColors[index] = visibleLight.finalColor;
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        //点光源的超出光照范围position平滑地过渡为0
        //避免远处镜面反射还能反射点光源
        position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightPositions[index] = position;

        otherLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);

        //saturate(da+b)^2
        //d = dot(_OtherLightDirections[index].xyz,light.direction)
        //a = 1 / (cos(ri/2)-cos(r0/2))
        //b = - cos(r0/2) * a
        //ri ,r0: inner,outter angle
        Light light = visibleLight.light;
        float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
        float outterCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
        float angleRangeInv = 1f / Mathf.Max(innerCos - outterCos, 0.001f);
        otherLightSpotAngles[index] = new Vector4(angleRangeInv, -outterCos * angleRangeInv);

        //阴影数据
        otherLightShadowData[index] = shadows.ReserveOtherShadows(light, index);
    }

    public void Cleanup()
    {
        shadows.Cleanup();
    }
}
