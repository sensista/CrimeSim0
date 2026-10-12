// CrimeSim – ShadowCaster / DepthOnly / DepthNormals für die Stil-Shader.
// Vorher muss der Shader CBUFFER + "half CSAlpha(float2 uv)" definieren.
// Alpha-Clip über Keyword _ALPHATEST_ON und Property _Cutoff.
#ifndef CRIMESIM_DEPTH_PASSES_INCLUDED
#define CRIMESIM_DEPTH_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

struct CSDepthAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
    float4 color      : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct CSDepthVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float3 normalWS   : TEXCOORD1;
    float4 color      : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

float3 _LightDirection;
float3 _LightPosition;

void CSClip(float2 uv, float4 vcolor)
{
#if defined(CS_BODY_HIDE)
    half hide = vcolor.a < 0.5 ? dot(saturate(vcolor.rgb), saturate(_HideMask.rgb)) : 0.0;
    clip(0.5 - hide);
#endif
#if defined(_ALPHATEST_ON)
    clip(CSAlpha(uv) - _Cutoff);
#endif
}

CSDepthVaryings CSShadowVert(CSDepthAttributes v)
{
    CSDepthVaryings o = (CSDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDir = normalize(_LightPosition - positionWS);
#else
    float3 lightDir = _LightDirection;
#endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
    o.positionCS = positionCS;
    o.uv = v.uv;
    o.color = v.color;
    o.normalWS = normalWS;
    return o;
}

half4 CSShadowFrag(CSDepthVaryings i) : SV_Target
{
    CSClip(i.uv, i.color);
    return 0;
}

CSDepthVaryings CSDepthVert(CSDepthAttributes v)
{
    CSDepthVaryings o = (CSDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
    o.uv = v.uv;
    o.color = v.color;
    o.normalWS = TransformObjectToWorldNormal(v.normalOS);
    return o;
}

half CSDepthFrag(CSDepthVaryings i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    CSClip(i.uv, i.color);
    return i.positionCS.z;
}

half4 CSDepthNormalsFrag(CSDepthVaryings i, bool isFrontFace : SV_IsFrontFace) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    CSClip(i.uv, i.color);
    float3 n = normalize(i.normalWS);
    n = isFrontFace ? n : -n;
#if defined(_GBUFFER_NORMALS_OCT)
    float2 oct = PackNormalOctQuadEncode(n);
    float2 rem = saturate(oct * 0.5 + 0.5);
    return half4(PackFloat2To888(rem), 0.0);
#else
    return half4(n, 0.0);
#endif
}
#endif
