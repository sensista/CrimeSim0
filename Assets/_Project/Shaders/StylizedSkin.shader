// CrimeSim/StylizedSkin – halb-realistische, weich gemalte Haut für URP 17 (Unity 6).
// Wrap-Diffuse + weiche Stufe, warmer Fake-SSS am Terminator, Rouge, Randlicht, weiches Glanzlicht.
// _BaseColor ist die Hautfarbe aus dem Creator (MPFBShapeDriver setzt sie per MaterialPropertyBlock).
// Ist _BaseColor == _ToneReference, wird die Textur unverändert gezeigt.
Shader "CrimeSim/StylizedSkin"
{
    Properties
    {
        [MainTexture] _BaseMap ("Haut-Textur", 2D) = "white" {}
        [MainColor] _BaseColor ("Hautfarbe (Creator)", Color) = (0.87, 0.69, 0.55, 1)
        _ToneReference ("Referenz-Hautfarbe der Textur", Color) = (0.87, 0.69, 0.55, 1)
        _ToneStrength ("Stärke Hautfarbe", Range(0, 1)) = 1

        [Header(Weiche Beleuchtung)]
        _Wrap ("Wrap (Licht um die Form)", Range(0, 1)) = 0.45
        _RampCenter ("Schattengrenze", Range(0, 1)) = 0.42
        _RampSoftness ("Weichheit Schattengrenze", Range(0.01, 0.5)) = 0.22
        _RampBlend ("Gemalte Stufe (0 weich, 1 hart)", Range(0, 1)) = 0.45
        _ShadowTint ("Schattenfarbe", Color) = (0.62, 0.42, 0.42, 1)
        _SSSColor ("Durchschein-Farbe (SSS)", Color) = (0.9, 0.35, 0.25, 1)
        _SSSStrength ("Durchschein-Stärke", Range(0, 1)) = 0.35
        _AmbientStrength ("Umgebungslicht", Range(0, 2)) = 1

        [Header(Rouge)]
        [NoScaleOffset] _BlushMask ("Rouge-Maske (R)", 2D) = "black" {}
        _BlushColor ("Rouge-Farbe", Color) = (0.88, 0.45, 0.42, 1)
        _BlushStrength ("Rouge-Stärke", Range(0, 1)) = 0

        [Header(Glanz und Randlicht)]
        _Smoothness ("Glätte", Range(0, 1)) = 0.45
        _SpecularStrength ("Glanzlicht", Range(0, 1)) = 0.12
        _RimColor ("Randlicht-Farbe", Color) = (1, 0.82, 0.7, 1)
        _RimStrength ("Randlicht-Stärke", Range(0, 2)) = 0.35
        _RimPower ("Randlicht-Breite", Range(0.5, 8)) = 3.5

        [Header(Normal Map)]
        [Toggle(_NORMALMAP)] _UseNormalMap ("Normal Map benutzen", Float) = 0
        [Normal][NoScaleOffset] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal-Stärke", Range(0, 2)) = 0.5

        [HideInInspector] _Cutoff ("Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Lit" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ToneReference;
            half _ToneStrength;
            half _Wrap;
            half _RampCenter;
            half _RampSoftness;
            half _RampBlend;
            half4 _ShadowTint;
            half4 _SSSColor;
            half _SSSStrength;
            half _AmbientStrength;
            half4 _BlushColor;
            half _BlushStrength;
            half _Smoothness;
            half _SpecularStrength;
            half4 _RimColor;
            half _RimStrength;
            half _RimPower;
            half _BumpScale;
            half _Cutoff;
        CBUFFER_END

        TEXTURE2D(_BaseMap);   SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BlushMask); SAMPLER(sampler_BlushMask);
        TEXTURE2D(_BumpMap);   SAMPLER(sampler_BumpMap);

        half CSAlpha(float2 uv) { return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv * _BaseMap_ST.xy + _BaseMap_ST.zw).a * _BaseColor.a; }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CSForwardVert
            #pragma fragment Frag

            #pragma shader_feature_local _NORMALMAP
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

            half3 CSShadeLight(Light light, CSSurface s)
            {
                half NdotL = dot(s.normalWS, light.direction);
                half atten = light.distanceAttenuation * lerp(1.0h, light.shadowAttenuation, 0.85h);
                half3 shade = CSSoftDiffuse(NdotL, _Wrap, _RampCenter, _RampSoftness, _RampBlend, _SSSColor.rgb, _SSSStrength);
                half3 diffuse = s.albedo * shade;
                half3 h = SafeNormalize(light.direction + s.viewDirWS);
                half specExp = exp2(10.0h * _Smoothness + 1.0h);
                half spec = pow(saturate(dot(s.normalWS, h)), specExp) * _SpecularStrength * saturate(NdotL * 4.0h);
                return (diffuse + spec.xxx) * light.color * atten;
            }

            #include "CrimeSimLightLoop.hlsl"

            half4 Frag(CSVaryings i, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float2 uv = i.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);

                half3 toneRatio = _BaseColor.rgb / max(_ToneReference.rgb, 0.05h);
                half3 albedo = tex.rgb * lerp(1.0h.xxx, toneRatio, _ToneStrength);
                half blush = SAMPLE_TEXTURE2D(_BlushMask, sampler_BlushMask, i.uv).r * _BlushStrength;
                albedo = lerp(albedo, albedo * _BlushColor.rgb * 1.4h, blush);
                albedo = saturate(albedo);

                half3 n = normalize(i.normalWS);
            #if defined(_NORMALMAP)
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                half3 t = normalize(i.tangentWS.xyz);
                half3 b = cross(n, t) * i.tangentWS.w;
                n = normalize(mul(nTS, half3x3(t, b, n)));
            #endif
                n = isFrontFace ? n : -n;

                CSSurface s = (CSSurface)0;
                s.positionWS = i.positionWS;
                s.normalWS = n;
                s.viewDirWS = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                s.albedo = albedo;

                half3 color = CSAccumulateLights(s, i.positionCS);
                // Umgebung: Schattenseite farbig statt grau
                color += SampleSH(n) * albedo * lerp(_ShadowTint.rgb, 1.0h.xxx, 0.35h) * _AmbientStrength;

                half NdotV = saturate(dot(n, s.viewDirWS));
                half rim = pow(1.0h - NdotV, _RimPower) * _RimStrength;
                color += rim * _RimColor.rgb * lerp(albedo, 1.0h.xxx, 0.5h);

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
            #pragma multi_compile_instancing
            #include "CrimeSimDepthPasses.hlsl"
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
