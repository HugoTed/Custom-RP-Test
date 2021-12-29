#ifndef CUSTOM_COMMON_INCLUDED
#define CUSTOM_COMMON_INCLUDED

#include "../../Library/PackageCache/com.unity.render-pipelines.core@10.6.0/ShaderLibrary/Common.hlsl"
#include "./UnityInput.hlsl"

//使用unity core rp的SpaceTransforms.hlsl，这部分不用
// float3 TransformObjectToWorld(float3 positionOS){
//     return mul(unity_ObjectToWorld,float4(positionOS,1.0)).xyz;
// }

// float4 TransformWorldToHClip(float3 positionWS){
//     return mul(unity_MatrixVP,float4(positionWS,1.0));
// }

//替换SpaceTransforms.hlsl中的矩阵声明
#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_MATRIX_P glstate_matrix_projection

#include "../../Library/PackageCache/com.unity.render-pipelines.core@10.6.0/ShaderLibrary/SpaceTransforms.hlsl"
#endif