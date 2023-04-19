Shader "Custom RP/Particles/Unlit"
{
	Properties{
		_BaseMap("Texture",2D) = "white"{}
		[HDR]_BaseColor("Color",Color) = (1,1,1,1)
		[Toggle(_VERTEX_COLORS)] _VertexColors("Vertex Colors", Float) = 0
		[Toggle(_FLIPBOOK_BLENDING)] _FlipbookBlending("Flipbook Blending", Float) = 0
		_Cutoff("Alpha Cutoff",Range(0,1)) = 0.5
		[Toggle(_CLIPPING)] _Clipping  ("Alpha Clipping",float) = 0
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend",float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend",float) = 0
		[Enum(Off,0,On,1)] _ZWrite("Z Write",float) = 1

		[Toggle(_NEAR_FADE)] _NearFade("Near Fade", Float) = 0
		_NearFadeDistance("Near Fade Distance", Range(0.0, 10.0)) = 1
		_NearFadeRange("Near Fade Range", Range(0.01, 10.0)) = 1
	}
	SubShader
	{
		HLSLINCLUDE
		#include "../ShaderLibrary/Common.hlsl"
		#include "UnlitInput.hlsl"
		ENDHLSL

		Pass
		{
			Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
			ZWrite [_ZWrite]

			HLSLPROGRAM
			#pragma target 3.5
			#pragma shader_feature _CLIPPING
			#pragma shader_feature _VERTEX_COLORS
			#pragma shader_feature _FLIPBOOK_BLENDING
			#pragma shader_feature _NEAR_FADE
			#pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE
			#pragma multi_compile_instancing
			#pragma vertex UnlitPassVertex
			#pragma fragment UnlitPassFragment
			#include "UnlitPass.hlsl"
			ENDHLSL
		}

		Pass{
			Tags{
				"LightMode" = "ShadowCaster"
			}
			ColorMask 0

			HLSLPROGRAM
			#pragma target 3.5
			#pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
			#pragma multi_compile_instancing
			#pragma vertex ShadowCasterPassVertex
			#pragma fragment ShadowCasterPassFragment
			#include "ShadowCasterPass.hlsl"
			ENDHLSL
		}
	}
	CustomEditor "CustomShaderGUI"
}