﻿//限制范围
#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED
//最小反射率，没有它，非金属将不会获得镜面高光
#define MIN_REFLECTIVITY 0.04
float OneMinusReflectivity(float metallic){
	float range = 1.0 - MIN_REFLECTIVITY;
	return range - metallic * range;
}

struct BRDF{
	float3 diffuse;
	float3 specular;
	float roughness;
	//感知粗糙度，用于访问正确mip级别的环境贴图
	float perceptualRoughness;
	float fresnel;
};


BRDF GetBRDF(Surface surface,bool applyAlphaToDiffuse = false){
	BRDF brdf;
	float oneMinusReflectivity = OneMinusReflectivity(surface.metallic);
	//金属度越高，漫反射越低
	brdf.diffuse = surface.color * oneMinusReflectivity;
	if(applyAlphaToDiffuse){
		brdf.diffuse *= surface.alpha;
	}
	//能量守恒，出射光不能超过入射光
	brdf.specular = lerp(MIN_REFLECTIVITY,surface.color,surface.metallic);
	brdf.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
	brdf.roughness = PerceptualRoughnessToRoughness(brdf.perceptualRoughness);
	brdf.fresnel = saturate(surface.smoothness + 1.0 - oneMinusReflectivity);
	return brdf;
}

float SpecularStrength(Surface surface,BRDF brdf,Light light){
	float3 h = SafeNormalize(light.direction + surface.viewDirection);
	float nh2 = Square(saturate(dot(surface.normal,h)));
	float lh2 = Square(saturate(dot(light.direction,h)));
	float r2 = Square(brdf.roughness);
	float d2 = Square(nh2 * (r2 - 1.0) + 1.00001);
	float normalization = brdf.roughness * 4.0 + 2.0;
	return r2 / (d2 * max(0.1,lh2) * normalization);
}

float3 DirectBRDF(Surface surface,BRDF brdf,Light light){
	return SpecularStrength(surface,brdf,light) * brdf.specular + brdf.diffuse;
}

float3 IndirectBRDF(
	Surface surface, BRDF brdf, float3 diffuse, float3 specular
) {
	float fresnelStrength = surface.fresnelStrength *
		Pow4(1.0 - saturate(dot(surface.normal, surface.viewDirection)));
	//镜面GI
	float3 reflection = specular * lerp(brdf.specular,brdf.fresnel, fresnelStrength);
	reflection /= brdf.roughness * brdf.roughness + 1.0;
	
	return (diffuse * brdf.diffuse + reflection) * surface.occlusion;
}

#endif