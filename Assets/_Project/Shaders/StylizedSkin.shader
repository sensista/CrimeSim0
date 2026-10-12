// CrimeSim/StylizedSkin – "gemalt, aber realistisch" nur mit glatten Mitteln (keine Pinsel/Rauschen) für URP 17.
// Realistische Form und Licht, dazu dezent gemalte Übergänge und Farbvariation.
//  - Gemalte Lichtbänder mit weichen, sichtbaren Übergängen; Pinselstriche stören Lichtgrenze und Farbe
//  - Farbtöne: Licht (Pfirsich) -> warmer rosiger Terminator -> Schatten mit warm/kühler Farbmischung
//  - Leicht entsättigte Mitteltöne, gemalte Farbvariation (Wangen/Nase/Ohren röter, Stirn gelber,
//    Kiefer/Augenhöhlen kühler), gemalte AO und Rouge, sehr dezentes gemaltes Glanzlicht
//  - Gegenlicht an dünnen Stellen (Ohren, Nase) über gebackene Dicken-Karte
// _BaseColor = Hautfarbe aus dem Creator (per MaterialPropertyBlock). Multiplikativ => hell bis dunkel.
Shader "CrimeSim/StylizedSkin"
{
    Properties
    {
        [MainTexture] _BaseMap ("Haut-Textur", 2D) = "white" {}
        [MainColor] _BaseColor ("Hautfarbe (Creator)", Color) = (0.87, 0.69, 0.55, 1)
        _ToneReference ("Referenz-Hautfarbe der Textur", Color) = (0.87, 0.69, 0.55, 1)
        _ToneStrength ("Stärke Hautfarbe", Range(0, 1)) = 1
        _Peach ("Pfirsich-Tönung", Color) = (1.0, 0.92, 0.88, 1)
        _Saturation ("Hautsättigung", Range(0, 1.5)) = 1.0

        [Header(Gemalte Lichtbaender)]
        _Wrap ("Wrap (Licht um die Form)", Range(0, 1)) = 0.5
        _TerminatorColor ("Terminator (warm, rosig)", Color) = (1.0, 0.64, 0.56, 1)
        _TerminatorWidth ("Terminator-Breite", Range(0.05, 0.6)) = 0.28
        _TerminatorStrength ("Terminator-Stärke", Range(0, 1)) = 0.6
        _ShadowTint ("Schatten kühl", Color) = (0.6, 0.52, 0.66, 1)
        _ShadowWarm ("Schatten warm", Color) = (0.72, 0.48, 0.44, 1)
        _ShadowStrength ("Eigenschatten-Stärke", Range(0, 1)) = 1
        _AmbientStrength ("Umgebungslicht", Range(0, 2)) = 1
        _ShadowLift ("Schatten aufhellen (nie schwarz)", Range(0, 0.3)) = 0.02


        [Header(Gemalte Farbvariation)]
        [NoScaleOffset] _VariationMap ("Variation (R rot, G gelb, B kühl)", 2D) = "black" {}
        _RedTint ("Rötung", Color) = (1.0, 0.72, 0.68, 1)
        _YellowTint ("Gelblich", Color) = (1.0, 0.95, 0.8, 1)
        _CoolTint ("Kühl", Color) = (0.86, 0.83, 0.93, 1)
        _VariationStrength ("Variation-Stärke", Range(0, 1)) = 0.6

        [Header(Gemalte Tiefe AO)]
        [NoScaleOffset] _AOMap ("AO-Map (gebacken)", 2D) = "white" {}
        _AOStrength ("AO-Stärke", Range(0, 1)) = 0.6
        _AOColor ("AO-Farbe (warm)", Color) = (0.66, 0.4, 0.38, 1)

        [Header(Rouge)]
        [NoScaleOffset] _BlushMask ("Rouge-Maske (R)", 2D) = "black" {}
        _BlushColor ("Rouge-Farbe", Color) = (0.95, 0.5, 0.48, 1)
        _BlushStrength ("Rouge-Stärke", Range(0, 1)) = 0.4

        [Header(Gegenlicht duenne Stellen)]
        [NoScaleOffset] _ThicknessMap ("Dünne Stellen (hell = dünn)", 2D) = "black" {}
        _TransColor ("Durchschein-Farbe", Color) = (1.0, 0.4, 0.3, 1)
        _TransStrength ("Durchschein-Stärke", Range(0, 2)) = 0.8

        [Header(Gemaltes Glanzlicht und Rand)]
        _Smoothness ("Glätte (breiter GGX-Glanz)", Range(0, 1)) = 0.6
        _SpecStrength ("Glanz-Stärke", Range(0, 3)) = 1.0
        _SheenStrength ("Fresnel-Schimmer (Umgebung)", Range(0, 1)) = 0.35
        _RimColor ("Randlicht-Farbe", Color) = (1, 0.82, 0.7, 1)
        _RimStrength ("Randlicht-Stärke", Range(0, 2)) = 0.3
        _RimPower ("Randlicht-Breite", Range(0.5, 8)) = 4

        [Header(Normal Map)]
        [Normal][NoScaleOffset] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal-Stärke (Form-Details)", Range(0, 2)) = 0.8
        [NoScaleOffset] _DetailMap ("Details (R Kavität, G Lippen)", 2D) = "gray" {}
        _CavityStrength ("Kavität-Tönung", Range(0, 1)) = 0.6
        _CavityColor ("Kavität-Farbe", Color) = (0.62, 0.38, 0.36, 1)
        _LipColor ("Lippen-Farbe", Color) = (0.9, 0.55, 0.52, 1)
        _LipStrength ("Lippen-Stärke", Range(0, 1)) = 0.5
        _LipBorder ("Lippenrand dunkler", Range(0, 1)) = 0.35
        _LipGloss ("Lippen-Glanz", Range(0, 3)) = 1.2

        [Header(Kleidung)]
        _HideMask ("Körper ausblenden (R Höschen, G Top, B Slip)", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Cutoff ("Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Lit" }
        LOD 300

        HLSLINCLUDE
        #define CS_BODY_HIDE 1
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ToneReference;
            half _ToneStrength;
            half4 _Peach;
            half _Saturation;
            half _Wrap;
            half4 _TerminatorColor;
            half _TerminatorWidth;
            half _TerminatorStrength;
            half4 _ShadowTint;
            half4 _ShadowWarm;
            half _ShadowStrength;
            half _AmbientStrength;
            half _ShadowLift;
            half4 _RedTint;
            half4 _YellowTint;
            half4 _CoolTint;
            half _VariationStrength;
            half _AOStrength;
            half4 _AOColor;
            half4 _BlushColor;
            half _BlushStrength;
            half4 _TransColor;
            half _TransStrength;
            half _Smoothness;
            half _SpecStrength;
            half _SheenStrength;
            half4 _RimColor;
            half _RimStrength;
            half _RimPower;
            half _BumpScale;
            half _CavityStrength;
            half4 _CavityColor;
            half4 _LipColor;
            half _LipStrength;
            half _LipBorder;
            half _LipGloss;
            float4 _HideMask;
            half _Cutoff;
        CBUFFER_END

        TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
        TEXTURE2D(_VariationMap); SAMPLER(sampler_VariationMap);
        TEXTURE2D(_AOMap);        SAMPLER(sampler_AOMap);
        TEXTURE2D(_BlushMask);    SAMPLER(sampler_BlushMask);
        TEXTURE2D(_ThicknessMap); SAMPLER(sampler_ThicknessMap);
        TEXTURE2D(_DetailMap);    SAMPLER(sampler_DetailMap);
        TEXTURE2D(_BumpMap);      SAMPLER(sampler_BumpMap);

        half CSAlpha(float2 uv) { return 1.0h; }
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

            // Pro Pixel gesetzt, in CSShadeLight benutzt
            static half gThin;
            static half gLip;

            // Glatte, weiche Licht-Rampe (kein Rauschen, keine Stufen)
            half SmoothRamp(half x) { return smoothstep(0.0h, 1.0h, x) * 0.6h + x * 0.4h; }

            half3 CSShadeLight(Light light, CSSurface s)
            {
                half NdotL = dot(s.normalWS, light.direction);
                half sh = lerp(1.0h, light.shadowAttenuation, _ShadowStrength);
                sh = lerp(sh, 1.0h, saturate(1.0h - abs(NdotL) * 3.0h) * 0.25h); // nur Schatten-Akne am Terminator dämpfen
                half w = saturate((NdotL + _Wrap) / (1.0h + _Wrap)) * sh;
                half lit = SmoothRamp(w);

                // Schatten leicht kühl, Terminator warm-rosig (rotverschoben)
                half3 shadowCol = lerp(_ShadowTint.rgb, _ShadowWarm.rgb, 0.35h);
                half3 tone = lerp(shadowCol, 1.0h.xxx, lit);
                half term = saturate(1.0h - abs(lit - 0.5h) / max(_TerminatorWidth, 0.01h));
                tone = lerp(tone, tone * _TerminatorColor.rgb * 1.25h, term * _TerminatorStrength);
                half3 col = s.albedo * tone * lit;

                // Gemaltes, weiches Glanzlicht
                half3 h = SafeNormalize(light.direction + s.viewDirWS);
                // Breiter, weicher GGX-Glanz (Haut F0 ~0.028) mit Schlick-Fresnel
                float rough = max(1.0 - _Smoothness, 0.08);
                float a2 = rough * rough * rough * rough;
                float NdotH = saturate(dot(s.normalWS, h));
                float dd = NdotH * NdotH * (a2 - 1.0) + 1.0;
                float D = a2 / (PI * dd * dd);
                half F = 0.028h + 0.972h * pow(1.0h - saturate(dot(light.direction, h)), 5.0h);
                half spec = (half)(D * 0.25) * F * saturate(NdotL) * sh * _SpecStrength * (1.0h + gLip * _LipGloss);
                col += spec.xxx;

                // Gegenlicht an dünnen Stellen
                half back = pow(saturate(dot(s.viewDirWS, -light.direction)), 3.0h);
                col += s.albedo * _TransColor.rgb * back * gThin * _TransStrength;

                return col * light.color * light.distanceAttenuation;
            }

            #include "CrimeSimLightLoop.hlsl"

            half4 Frag(CSVaryings i, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                CSBodyHideClip(i.color);
                float2 uv = i.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                half3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;

                half3 toneRatio = _BaseColor.rgb / max(_ToneReference.rgb, 0.05h);
                half3 albedo = tex * lerp(1.0h.xxx, toneRatio, _ToneStrength) * _Peach.rgb;

                half3 v = SAMPLE_TEXTURE2D(_VariationMap, sampler_VariationMap, i.uv).rgb * _VariationStrength;
                albedo *= lerp(1.0h.xxx, _RedTint.rgb, v.r);
                albedo *= lerp(1.0h.xxx, _YellowTint.rgb, v.g);
                albedo *= lerp(1.0h.xxx, _CoolTint.rgb, v.b);


                half blush = SAMPLE_TEXTURE2D(_BlushMask, sampler_BlushMask, i.uv).r * _BlushStrength;
                albedo = lerp(albedo, albedo * _BlushColor.rgb * 1.35h, blush);
                half ao = SAMPLE_TEXTURE2D(_AOMap, sampler_AOMap, i.uv).r;
                albedo *= lerp(1.0h.xxx, lerp(_AOColor.rgb, 1.0h.xxx, ao), _AOStrength);

                // Form-Details: Kavität (weich, warm) + Lippen mit dunklerem Rand
                half2 det = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, i.uv).rg;
                half cav = saturate((0.5h - det.r) * 2.0h);
                albedo *= lerp(1.0h.xxx, _CavityColor.rgb, cav * _CavityStrength);
                gLip = det.g;
                albedo = lerp(albedo, albedo * _LipColor.rgb * 1.2h, det.g * _LipStrength);
                half border = saturate(det.g * (1.0h - det.g) * 4.0h);
                albedo *= 1.0h - border * _LipBorder * 0.5h;
                half lum = dot(albedo, half3(0.3h, 0.59h, 0.11h));
                albedo = saturate(lerp(lum.xxx, albedo, _Saturation));
                gThin = SAMPLE_TEXTURE2D(_ThicknessMap, sampler_ThicknessMap, i.uv).r;

                half3 n = normalize(i.normalWS);
                half3 t = normalize(i.tangentWS.xyz);
                half3 bt = cross(n, t) * i.tangentWS.w;
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                n = normalize(mul(nTS, half3x3(t, bt, n)));
                n = isFrontFace ? n : -n;

                CSSurface s = (CSSurface)0;
                s.positionWS = i.positionWS;
                s.normalWS = n;
                s.viewDirWS = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                s.albedo = albedo;

                half3 color = CSAccumulateLights(s, i.positionCS);
                half3 shadowCol = lerp(_ShadowTint.rgb, _ShadowWarm.rgb, 0.35h);
                color += SampleSH(n) * albedo * lerp(shadowCol, 1.0h.xxx, 0.4h) * _AmbientStrength;
                color = max(color, albedo * shadowCol * _ShadowLift);

                half NdotV = saturate(dot(n, s.viewDirWS));
                color += pow(1.0h - NdotV, _RimPower) * _RimStrength * _RimColor.rgb * albedo;
                // Glatter Fresnel-Schimmer aus der Umgebung (glänzende, weiche Haut)
                half fr = 0.028h + 0.972h * pow(1.0h - NdotV, 5.0h);
                color += SampleSH(reflect(-s.viewDirWS, n)) * fr * _SheenStrength * _Smoothness;

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
