//限制范围
#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_OTHER_LIGHT_COUNT 64

CBUFFER_START(_CustomLight)
	int _DirectionalLightCount;
	float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightDirectionsAndMasks[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];

	int _OtherLightCount;
	float4 _OtherLightColors[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightPositions[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightDirectionsAndMasks[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightSpotAngles[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightShadowData[MAX_OTHER_LIGHT_COUNT];
CBUFFER_END


struct Light{
	float3 color;
	float3 direction;
	float attenuation;
    uint renderingLayerMask;
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
    light.direction = _DirectionalLightDirectionsAndMasks[index].xyz;
    light.renderingLayerMask = asuint(_DirectionalLightDirectionsAndMasks[index].w);
	DirectionalShadowData dirShadowData = GetDirectionalShadowData(index,shadowData);
	light.attenuation = GetDirectionalShadowAttenuation(dirShadowData,shadowData,surfaceWS);
	
	return light;
}

//Other Light
int GetOtherLightCount(){
	return _OtherLightCount;
}

OtherShadowData GetOtherShadowData(int lightIndex){
	OtherShadowData data;
	data.strength = _OtherLightShadowData[lightIndex].x;
	data.tileIndex = _OtherLightShadowData[lightIndex].y;
	data.shadowMaskChannel = _OtherLightShadowData[lightIndex].w;
	data.isPoint = _OtherLightShadowData[lightIndex].z == 1.0;
	data.lightPositionWS = 0.0;
	data.lightDirectionWS = 0.0;
	data.spotDirectionWS = 0.0;
	return data;
}

Light GetOtherLight(int index,Surface surfaceWS,ShadowData shadowData)
{
	Light light;
	light.color = _OtherLightColors[index].rgb;
	float3 position = _OtherLightPositions[index].xyz;
	float3 ray = position - surfaceWS.position;
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
	// Spot light Angle
	//saturate(da+b)^2
	//d = dot(_OtherLightDirections[index].xyz,light.direction)
	//a = 1 / (cos(ri/2)-cos(r0/2))
	//b = - cos(r0/2) * a
	//ri ,r0: inner,outter angle
    float3 spotDirection = _OtherLightDirectionsAndMasks[index].xyz;
    light.renderingLayerMask = asuint(_OtherLightDirectionsAndMasks[index].w);
	float4 spotAngles = _OtherLightSpotAngles[index];
	float spotAttenuation = Square(
		saturate(dot(spotDirection,light.direction) *
		spotAngles.x + spotAngles.y)
		);

	OtherShadowData otherShadowData = GetOtherShadowData(index);
	otherShadowData.lightPositionWS = position;
	otherShadowData.lightDirectionWS = light.direction;
	otherShadowData.spotDirectionWS = spotDirection;
	light.attenuation = 
		GetOtherShadowAttenuation(otherShadowData,shadowData,surfaceWS ) *
		spotAttenuation * rangeAttenuation / distanceSqr;
	
	return light;
}


#endif