﻿//限制范围
#ifndef CUSTOM_UNLIT_INPUT_INCLUDED
#define CUSTOM_UNLIT_INPUT_INCLUDED

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(Props, name)

TEXTURE2D(_BaseMap);
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_BaseMap);

UNITY_INSTANCING_BUFFER_START(Props)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeDistance)
	UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeRange)
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
	UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)
UNITY_INSTANCING_BUFFER_END(Props)

struct InputConfig {
    Fragment fragment;
    float4 color;
	float2 baseUV;
	float2 detailUV;
    float3 flipbookUVB;
    bool flipbookBlending;
    bool nearFade;
};

float GetFinalAlpha(float alpha)
{
    return INPUT_PROP(_ZWrite) ? 1.0 : alpha;
}

InputConfig GetInputConfig(float4 positionSS,float2 baseUV, float2 detailUV = 0.0) {
	InputConfig c;
    c.fragment = GetFragment(positionSS);
    c.color = 1.0;
	c.baseUV = baseUV;
	c.detailUV = detailUV;
    c.flipbookUVB = 0.0;
    c.flipbookBlending = false;
    c.nearFade = false;
	return c;
}

float2 TransformBaseUV(float2 baseUV) {
	float4 baseST = INPUT_PROP(_BaseMap_ST);
	return baseUV * baseST.xy + baseST.zw;
}

float4 GetBase(InputConfig c) {
	float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.baseUV);
    if (c.flipbookBlending)
    {
        map = lerp(
			map, SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.flipbookUVB.xy),
			c.flipbookUVB.z
		);
    }
    if (c.nearFade)
    {
        float nearAttenuation = (c.fragment.depth - INPUT_PROP(_NearFadeDistance)) /
			INPUT_PROP(_NearFadeRange);
        map.a *= saturate(nearAttenuation);
    }
	float4 color = INPUT_PROP(_BaseColor);
    return map * color * c.color;
}

float GetCutoff(InputConfig c) {
	return INPUT_PROP(_Cutoff);
}

float GetMetallic(InputConfig c) {
	return 0.0;
}

float GetSmoothness(InputConfig c) {
	return 0.0;
}

float3 GetEmission(InputConfig c) {
	float4 map = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, c.baseUV);
	float4 color = INPUT_PROP(_EmissionColor);
	return map.rgb * color.rgb;
}

float GetFresnel(InputConfig c) {
	return 0.0;
}
#endif