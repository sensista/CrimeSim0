// CrimeSim – Lichtschleife. Vorher muss der Shader
//   half3 CSShadeLight(Light light, CSSurface s)
// definieren. Unterstützt Forward, Forward+ (_FORWARD_PLUS) und Cluster-Light-Loop (_CLUSTER_LIGHT_LOOP).
#ifndef CRIMESIM_LIGHT_LOOP_INCLUDED
#define CRIMESIM_LIGHT_LOOP_INCLUDED

#if (defined(USE_CLUSTER_LIGHT_LOOP) && USE_CLUSTER_LIGHT_LOOP) || (defined(USE_FORWARD_PLUS) && USE_FORWARD_PLUS)
    #define CS_CLUSTERED 1
#else
    #define CS_CLUSTERED 0
#endif

half3 CSAccumulateLights(CSSurface s, float4 positionCS)
{
    half4 shadowMask = half4(1, 1, 1, 1);
    float4 shadowCoord = TransformWorldToShadowCoord(s.positionWS);
    Light mainLight = GetMainLight(shadowCoord, s.positionWS, shadowMask);
    half3 color = CSShadeLight(mainLight, s);

#if defined(_ADDITIONAL_LIGHTS) || CS_CLUSTERED
    // Variablen für LIGHT_LOOP_BEGIN (erwartet "inputData")
    InputData inputData = (InputData)0;
    inputData.positionWS = s.positionWS;
    inputData.normalWS = s.normalWS;
    inputData.viewDirectionWS = s.viewDirWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);

    #if CS_CLUSTERED
    for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
    {
        Light dl = GetAdditionalLight(dirIndex, s.positionWS, shadowMask);
        color += CSShadeLight(dl, s);
    }
    #endif

    uint pixelLightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, s.positionWS, shadowMask);
        color += CSShadeLight(light, s);
    LIGHT_LOOP_END
#endif
    return color;
}
#endif
