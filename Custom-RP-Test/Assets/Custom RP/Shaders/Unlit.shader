Shader "Custom RP/Unlit"
{
	Properties{
		_BaseColor("Color",Color) = (1,1,1,1)
	}
	SubShader
	{
		Pass
		{
			HLSLPROGRAM
			#pragma vertex UnlitPassVertex
			#pragma fragment UnlitPassFragment
			#include "UnlitPass.hlsl"
			ENDHLSL
		}
	}
}