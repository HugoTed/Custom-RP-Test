using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using LightType = UnityEngine.LightType;

public partial class CustomRenderPipeline
{
    partial void InitializeForEditor();

    partial void DisposeForEditor();

#if UNITY_EDITOR

    partial void InitializeForEditor()
    {
        //设置委托
        Lightmapping.SetDelegate(lightsDelegate);
    }

    partial void DisposeForEditor()
    {
        //管线被弃置时要重置清理委托
        Lightmapping.ResetDelegate();
    }

    //覆盖灯光的烘焙数据
    static Lightmapping.RequestLightsDelegate lightsDelegate =
        (Light[] lights, NativeArray<LightDataGI> output) => {
            var lightData = new LightDataGI();
            for (int i = 0; i < lights.Length; i++)
            {
                var light = lights[i];
                switch (light.type)
                {
                    case LightType.Directional:
                        var directionalLight = new DirectionalLight();
                        LightmapperUtils.Extract(light, ref directionalLight);
                        lightData.Init(ref directionalLight);
                        break;
                    case LightType.Point:
                        var pointLight = new PointLight();
                        LightmapperUtils.Extract(light, ref pointLight);
                        lightData.Init(ref pointLight);
                        break;
                    case LightType.Spot:
                        var spotLight = new SpotLight();
                        LightmapperUtils.Extract(light, ref spotLight);
                        lightData.Init(ref spotLight);

                        //for unity 2019.3
                        //var spotLight = new SpotLight();
                        //LightmapperUtils.Extract(light, ref spotLight);
                        //spotLight.innerConeAngle =
                        //    light.innerSpotAngle * Mathf.Deg2Rad;
                        //spotLight.angularFalloff =
                        //    AngularFalloffType.AnalyticAndInnerAngle;
                        //lightData.Init(ref spotLight);
                        break;
                    case LightType.Area:
                        var areaLight = new RectangleLight();
                        LightmapperUtils.Extract(light, ref areaLight);
                        areaLight.mode = LightMode.Baked;
                        lightData.Init(ref areaLight);
                        break;
                    default:
                        lightData.InitNoBake(light.GetInstanceID());
                        break;
                }
                //为所有灯光设置灯光数据的衰减类型，避免烘焙出来太亮
                lightData.falloff = FalloffType.InverseSquared;
                output[i] = lightData;
            }
        };
#endif
}
