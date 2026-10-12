// CrimeSim/StylizedHair – Haarkarten mit Alpha-Clip, Kajiya-Kay-Glanzband, Randlicht und Tönung.
Shader "CrimeSim/StylizedHair"
{
    Properties
    {
        [MainTexture] _BaseMap ("Haar-Textur (RGB + Alpha)", 2D) = "white" {}
        [MainColor] _BaseColor ("Haarfarbe", Color) = (0.25, 0.16, 0.1, 1)
        _Cutoff ("Alpha-Clip", Range(0, 1)) = 0.4
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha-Clip aktiv", Float) = 1
        _Wrap ("Wrap", Range(0, 1)) = 0.5
        _AmbientStrength ("Umgebungslicht", Range(0, 2)) = 1
        [ToggleUI] _UseBitangent ("Strähnen entlang Bitangente (V)", Float) = 1
        _SpecColor1 ("Glanz 1 Farbe", Color) = (1, 0.92, 0.82, 1)
        _SpecShift1 ("Glanz 1 Verschiebung", Range(-1, 1)) = 0.1
        _SpecExp1 ("Glanz 1 Schärfe", Range(1, 512)) = 80
        _SpecStrength1 ("Glanz 1 Stärke", Range(0, 2)) = 0.35
        _SpecColor2 ("Glanz 2 Farbe", Color) = (1, 0.75, 0.55, 1)
        _SpecShift2 ("Glanz 2 Verschiebung", Range(-1, 1)) = -0.15
        _SpecExp2 ("Glanz 2 Schärfe", Range(1, 512)) = 16
        _SpecStrength2 ("Glanz 2 Stärke", Range(0, 2)) = 0.2
        _RimColor ("Randlicht-Farbe", Color) = (1, 0.85, 0.7, 1)
        _RimStrength ("Randlicht-Stärke", Range(0, 2)) = 0.4
        _RimPower ("Randlicht-Breite", Range(0.5, 8)) = 3
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            half _Wrap;
            half _AmbientStrength;
            half _UseBitangent;
            half4 _SpecColor1;
            half _SpecShift1;
            half _SpecExp1;
            half _SpecStrength1;
            half4 _SpecColor2;
            half _SpecShift2;
            half _SpecExp2;
            half _SpecStrength2;
            half4 _RimColor;
            half _RimStrength;
            half _RimPower;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        half CSAlpha(float2 uv) { return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv * _BaseMap_ST.xy + _BaseMap_ST.zw).a * _BaseColor.a; }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On
            HLSLPROGRAM
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma target 3.5
            #pragma vertex CSForwardVert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "CrimeSimStylizedCommon.hlsl"

            half KajiyaSpec(half3 T, half3 N, half3 H, half shift, half exponent)
            {
                half3 t = normalize(T + N * shift);
                half TdotH = dot(t, H);
                half sinTH = sqrt(max(0.0h, 1.0h - TdotH * TdotH));
                return smoothstep(-1.0h, 0.0h, TdotH) * pow(sinTH, exponent);
            }

            half3 CSShadeLight(Light light, CSSurface s)
            {
                half NdotL = dot(s.normalWS, light.direction);
                half w = saturate((NdotL + _Wrap) / (1.0h + _Wrap));
                half atten = light.distanceAttenuation * lerp(1.0h, light.shadowAttenuation, 0.8h);
                half3 h = SafeNormalize(light.direction + s.viewDirWS);
                half3 spec = _SpecColor1.rgb * KajiyaSpec(s.strandWS, s.normalWS, h, _SpecShift1, _SpecExp1) * _SpecStrength1
                           + _SpecColor2.rgb * s.albedo * KajiyaSpec(s.strandWS, s.normalWS, h, _SpecShift2, _SpecExp2) * _SpecStrength2;
                return (s.albedo * w + spec * saturate(NdotL * 2.0h)) * light.color * atten;
            }

            #include "CrimeSimLightLoop.hlsl"

            half4 Frag(CSVaryings i, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv * _BaseMap_ST.xy + _BaseMap_ST.zw);
                half alpha = tex.a * _BaseColor.a;
            #if defined(_ALPHATEST_ON)
                clip(alpha - _Cutoff);
            #endif
                half3 n = normalize(i.normalWS);
                n = isFrontFace ? n : -n;
                half3 t = normalize(i.tangentWS.xyz);
                half3 b = normalize(cross(normalize(i.normalWS), t) * i.tangentWS.w);

                CSSurface s = (CSSurface)0;
                s.positionWS = i.positionWS;
                s.normalWS = n;
                s.viewDirWS = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                s.strandWS = _UseBitangent > 0.5h ? b : t;
                s.albedo = saturate(tex.rgb * _BaseColor.rgb);

                half3 color = CSAccumulateLights(s, i.positionCS);
                color += SampleSH(n) * s.albedo * _AmbientStrength;
                half rim = pow(1.0h - saturate(dot(n, s.viewDirWS)), _RimPower) * _RimStrength;
                color += rim * _RimColor.rgb * s.albedo * 2.0h;
                color = MixFog(color, i.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CSShadowVert
            #pragma fragment CSShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include "CrimeSimDepthPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CSDepthVert
            #pragma fragment CSDepthFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include "CrimeSimDepthPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CSDepthVert
            #pragma fragment CSDepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include "CrimeSimDepthPasses.hlsl"
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
