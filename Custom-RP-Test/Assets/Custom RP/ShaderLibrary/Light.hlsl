//限制范围
#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_OTHER_LIGHT_COUNT 64

CBUFFER_START(_CustomLight)
	int _DirectionalLightCount;
	float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];

	int _OtherLightCount;
	float4 _OtherLightColors[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightPositions[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightDirections[MAX_OTHER_LIGHT_COUNT];
CBUFFER_END


struct Light{
	float3 color;
	float3 direction;
	float attenuation;
};

//Directional Light
DirectionalShadowData GetDirectionalShadowData(
	int lightIndex, ShadowData shadowData
){
	DirectionalShadowData data;
	data.strength = _DirectionalLightShadowData[lightIndex].x;// * shadowData.strength;
	data.tileIndex = 
		_DirectionalLightShadowData[lightIndex].y + shadowData.cascadeIndex;
	data.normalBias = _DirectionalLightShadowData[lightIndex].z;
	data.shadowMaskChannel = _DirectionalLightShadowData[lightIndex].w;
	return data;
}

int GetDirectionalLightCount(){
	return _DirectionalLightCount;
}

Light GetDirectionalLighting(int index,Surface surfaceWS,ShadowData shadowData)
{
	Light light;
	light.color = _DirectionalLightColors[index].rgb;
	light.direction = _DirectionalLightDirections[index].xyz;
	DirectionalShadowData dirShadowData = GetDirectionalShadowData(index,shadowData);
	light.attenuation = GetDirectionalShadowAttenuation(dirShadowData,shadowData,surfaceWS);
	
	return light;
}

//Other Light
int GetOtherLightCount(){
	return _OtherLightCount;
}

Light GetOtherLight(int index,Surface surfaceWS,ShadowData shadowData)
{
	Light light;
	light.color = _OtherLightColors[index].rgb;
	float3 ray = _OtherLightPositions[index].xyz - surfaceWS.position;
	light.direction = normalize(ray);

	//point light Attenuation
	// intensity = i / d
	float distanceSqr = max(dot(ray,ray),0.00001);
	//clamp light range
	//max(0,1-(d^2/r^2)^2)^2
	float rangeAttenuation = Square(
		saturate(1.0 - Square(distanceSqr * _OtherLightPositions[index].w))
		);

	//Spot light Attenuation
	float spotAttenuation = 
		saturate(dot(_OtherLightDirections[index].xyz,light.direction));

	light.attenuation = spotAttenuation * rangeAttenuation / distanceSqr;
	
	return light;
}


#endif