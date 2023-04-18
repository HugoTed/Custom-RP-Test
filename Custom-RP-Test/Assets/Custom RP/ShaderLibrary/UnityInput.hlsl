//限制范围
#ifndef CUSTOM_UNITY_INPUT_INCLUDED
#define CUSTOM_UNITY_INPUT_INCLUDED

CBUFFER_START(UnityPerDraw)
float4x4 unity_ObjectToWorld;
float4x4 unity_WorldToObject;
float4 unity_LODFade;
real4 unity_WorldTransformParams;

float4 unity_RenderingLayer;

//灯光数量包含在y分量中
real4 unity_LightData;
//每个通道包含一个灯光索引
//每个对象最多支持8个灯光
real4 unity_LightIndices[2];

float4 unity_ProbesOcclusion;
float4 unity_SpecCube0_HDR;
float4 unity_LightmapST;
float4 unity_DynamicLightmapST;

float4 unity_SHAr;
float4 unity_SHAg;
float4 unity_SHAb;
float4 unity_SHBr;
float4 unity_SHBg;
float4 unity_SHBb;
float4 unity_SHC;

float4 unity_ProbeVolumeParams;
float4x4 unity_ProbeVolumeWorldToObject;
float4 unity_ProbeVolumeSizeInv;
float4 unity_ProbeVolumeMin;

CBUFFER_END

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 glstate_matrix_projection;
float3 _WorldSpaceCameraPos;

float4 _ProjectionParams;
float4 unity_OrthoParams;
#endif