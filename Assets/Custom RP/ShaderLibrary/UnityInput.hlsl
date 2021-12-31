#ifndef CUSTOM_UNITY_INPUT_INCLUDED
#define CUSTOM_UNLIT_INPUT_INCLUDED

CBUFFER_START(UnityPerDraw)
    float4x4 unity_ObjectToWorld;
    float4x4 unity_WorldToObject;
    float4 unity_LODFade;
    //real4并不是一个有效类型，他是float4或half4的别名，取决于目标平台
    real4 unity_WorldTransformParams;
CBUFFER_END

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 glstate_matrix_projection;
//有些图形api的uv中的v是顶部开始的，有些是底部，所以用这个参数来计算是否要变换
float4 _ProjectionParams;

#endif