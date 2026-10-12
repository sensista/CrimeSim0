// CrimeSim/StylizedEye – große, glänzende Augen mit gemaltem Lichtreflex (Catchlight).
// Die Iris wird über die Helligkeit der Textur erkannt und kann umgefärbt werden.
Shader "CrimeSim/StylizedEye"
{
    Properties
    {
        [MainTexture] _BaseMap ("Augen-Textur", 2D) = "white" {}
        [MainColor] _BaseColor ("Tönung", Color) = (1, 1, 1, 1)
        _IrisColor ("Irisfarbe", Color) = (0.35, 0.22, 0.12, 1)
        _IrisTint ("Iris umfärben", Range(0, 1)) = 0
        _IrisThreshold ("Iris-Erkennung (Helligkeit)", Range(0.2, 0.95)) = 0.7
        _ScleraBrightness ("Augenweiß Helligkeit", Range(0.5, 1.5)) = 1.0
        _Wrap ("Wrap", Range(0, 1)) = 0.3
        _AmbientStrength ("Umgebungslicht", Range(0, 2)) = 0.7
        _Smoothness ("Glätte", Range(0, 1)) = 0.95
        _SpecularStrength ("Glanzlicht", Range(0, 3)) = 0.9
        _CatchlightDir ("Catchlight-Richtung (Kamera-Raum)", Vector) = (-0.35, 0.45, 1, 0)
        _CatchlightSize ("Catchlight-Größe", Range(0.8, 0.999)) = 0.95
        _CatchlightStrength ("Catchlight-Stärke", Range(0, 3)) = 0.9
        _InnerGlow ("Iris-Leuchten (Gegenlicht)", Range(0, 1)) = 0.05
        [HideInInspector] _Cutoff ("Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _IrisColor;
            half _IrisTint;
            half _IrisThreshold;
            half _ScleraBrightness;
            half _Wrap;
            half _AmbientStrength;
            half _Smoothness;
            half _SpecularStrength;
            float4 _CatchlightDir;
            half _CatchlightSize;
            half _CatchlightStrength;
            half _InnerGlow;
            half _Cutoff;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        half CSAlpha(float2 uv) { return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv * _BaseMap_ST.xy + _BaseMap_ST.zw).a; }
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
                half w = saturate((NdotL + _Wrap) / (1.0h + _Wrap));
                half atten = light.distanceAttenuation * lerp(1.0h, light.shadowAttenuation, 0.6h);
                half3 h = SafeNormalize(light.direction + s.viewDirWS);
                half spec = pow(saturate(dot(s.normalWS, h)), exp2(10.0h * _Smoothness + 1.0h)) * _SpecularStrength;
                return (s.albedo * w + spec.xxx) * light.color * atten;
            }

            #include "CrimeSimLightLoop.hlsl"

            half4 Frag(CSVaryings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv * _BaseMap_ST.xy + _BaseMap_ST.zw);
                half lum = dot(tex.rgb, half3(0.299h, 0.587h, 0.114h));
                half iris = saturate((_IrisThreshold - lum) * 4.0h);
                half3 albedo = tex.rgb * _BaseColor.rgb;
                half3 sclera = lerp(albedo, dot(albedo, half3(0.333h, 0.333h, 0.333h)).xxx * half3(1.0h, 0.98h, 0.96h), 0.85h) * _ScleraBrightness;
                albedo = lerp(sclera, albedo, iris);
                albedo = lerp(albedo, _IrisColor.rgb * (0.4h + lum * 1.6h), iris * _IrisTint);
                albedo = saturate(albedo);

                CSSurface s = (CSSurface)0;
                s.positionWS = i.positionWS;
                s.normalWS = normalize(i.normalWS);
                s.viewDirWS = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                s.albedo = albedo;

                half3 color = CSAccumulateLights(s, i.positionCS);
                color += SampleSH(s.normalWS) * albedo * _AmbientStrength;
                // Iris leicht von innen aufhellen (wirkt lebendiger)
                color += albedo * iris * _InnerGlow;

                // Gemalter Lichtreflex: Reflexionsvektor im Kamera-Raum gegen feste Richtung
                half3 rWS = reflect(-s.viewDirWS, s.normalWS);
                half3 rVS = normalize(mul((float3x3)UNITY_MATRIX_V, rWS));
                half d = dot(rVS, normalize(_CatchlightDir.xyz));
                half catchlight = smoothstep(_CatchlightSize, _CatchlightSize + 0.008h, d);
                color += catchlight * _CatchlightStrength;

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
