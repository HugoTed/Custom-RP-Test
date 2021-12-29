//除了代码块的作用域外只有全局作用域
//用include的话就是把整个hlsl的代码包含进来，所以要设定包含保护include gaurd
//为了不被再次包含，所以要判断是否已被定义
#ifndef CUSTOM_UNLIT_PASS_INCLUDED
#define CUSTOM_UNLIT_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"
//SRP Batch
CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
CBUFFER_END

float4 UnlitPassVertex(float3 positionOS : POSITION) : SV_POSITION {
    float3 positionWS = TransformObjectToWorld(positionOS.xyz);

    return TransformWorldToHClip(positionWS);
}

float4 UnlitPassFragment() : SV_TARGET{
    return _BaseColor;
}

#endif
