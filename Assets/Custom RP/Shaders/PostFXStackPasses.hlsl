#ifndef CUSTOM_POST_FX_PASSES_INCLUDED
#define CUSTOM_POST_FX_PASSES_INCLUDED

TEXTURE2D(_PostFXSource);
SAMPLER(sampler_linear_clamp);

float4 GetSource(float2 screenUV){
    //因为我们的buffer永远不会有mipmap，所以我们用SAMPLE_TEXTURE2D_LOD
    //来代替SAMPLE_TEXTURE2D，并把最后的参数设置为0，表示强制mipmap级别为0
    return SAMPLE_TEXTURE2D_LOD(_PostFXSource,sampler_linear_clamp,screenUV,0);
}


struct Varyings{
    float4 positionCS : SV_POSITION;
    float2 screenUV : VAR_SCREEN_UV;
};

//画一个矩形代替原本屏幕后处理特效的两个三角形组成的矩形
Varyings DefaultPassVertex(uint vertexID : SV_VertexID){
    Varyings output;
    output.positionCS = float4(
        vertexID <= 1? -1.0 : 3.0,
        vertexID == 1? 3.0 : -1.0,
        0.0,1.0
    );
    output.screenUV = float2(
        vertexID <= 1? 0.0 : 2.0,
        vertexID == 1? 2.0 : 0.0
    );
    if(_ProjectionParams.x<0.0){
        output.screenUV.y = 1.0 - output.screenUV.y;
    }
    return output;
}

float4 CopyPassFragment(Varyings input) : SV_TARGET{
    return GetSource(input.screenUV);
}

#endif