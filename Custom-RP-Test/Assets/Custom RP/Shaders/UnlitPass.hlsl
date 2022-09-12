//限制范围
#ifndef CUSTOM_UNLIT_PASS_INCLUDED
#define CUSTOM_UNLIT_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"

float4 _BaseColor;

float4 UnlitPassVertex(float3 positionOS : POSITION) : SV_POSITION 
{
	float3 positionWS = TransformObjectToWorld(positionOS.xyz);
	float4 positionCS = TransformWorldToHClip(positionWS);
	return positionCS;
}
float4 UnlitPassFragment(): SV_TARGET
{
	return _BaseColor;
}

#endif