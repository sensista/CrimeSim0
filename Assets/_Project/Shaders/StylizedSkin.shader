// CrimeSim/StylizedSkin – halb-realistische Haut mit regelbarem Malanteil (_Painterly 0..1, Standard 0.35) für URP 17.
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
        _Painterly ("Malerisch (0 sauber, 1 stark gemalt)", Range(0, 1)) = 0.35
        [MainTexture] _BaseMap ("Haut-Textur", 2D) = "white" {}
        [MainColor] _BaseColor ("Hautfarbe (Creator)", Color) = (0.87, 0.69, 0.55, 1)
        _ToneReference ("Referenz-Hautfarbe der Textur", Color) = (0.87, 0.69, 0.55, 1)
        _ToneStrength ("Stärke Hautfarbe", Range(0, 1)) = 1
        _Peach ("Pfirsich-Tönung", Color) = (1.0, 0.94, 0.87, 1)
        _Saturation ("Hautsättigung", Range(0, 1.5)) = 0.95
        _MidDesaturate ("Mitteltöne entsättigen", Range(0, 1)) = 0.25

        [Header(Gemalte Lichtbaender)]
        _Wrap ("Wrap (Licht um die Form)", Range(0, 1)) = 0.5
        _Bands ("Anzahl Lichtbänder", Range(1, 6)) = 3
        _BandSoftness ("Weichheit der Bänder", Range(0.02, 1)) = 0.4
        _BandBlend ("Bänder sichtbar (bei Malerisch=1)", Range(0, 1)) = 0.8
        _TerminatorColor ("Terminator (warm, rosig)", Color) = (1.0, 0.64, 0.56, 1)
        _TerminatorWidth ("Terminator-Breite", Range(0.05, 0.6)) = 0.28
        _TerminatorStrength ("Terminator-Stärke", Range(0, 1)) = 0.6
        _ShadowTint ("Schatten kühl", Color) = (0.6, 0.52, 0.66, 1)
        _ShadowWarm ("Schatten warm", Color) = (0.72, 0.48, 0.44, 1)
        _ShadowStrength ("Eigenschatten-Stärke", Range(0, 1)) = 0.45
        _AmbientStrength ("Umgebungslicht", Range(0, 2)) = 1
        _ShadowLift ("Schatten aufhellen (nie schwarz)", Range(0, 0.3)) = 0.1

        [Header(Pinselstriche Oel)]
        [NoScaleOffset] _BrushMap ("Pinselstriche (kachelbar)", 2D) = "gray" {}
        _BrushScale ("Pinsel-Kachelung", Range(1, 40)) = 7
        _BrushColor ("Pinsel auf Farbe", Range(0, 0.5)) = 0.1
        _BrushLight ("Pinsel auf Lichtgrenze", Range(0, 0.5)) = 0.18
        _BrushNormal ("Pinsel-Relief (Impasto)", Range(0, 1)) = 0.15

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
        _HighlightSize ("Glanzlicht-Größe", Range(0.5, 0.99)) = 0.9
        _HighlightStrength ("Glanzlicht-Stärke", Range(0, 0.5)) = 0.05
        _RimColor ("Randlicht-Farbe", Color) = (1, 0.82, 0.7, 1)
        _RimStrength ("Randlicht-Stärke", Range(0, 2)) = 0.15
        _RimPower ("Randlicht-Breite", Range(0.5, 8)) = 4

        [Header(Normal Map)]
        [Toggle(_NORMALMAP)] _UseNormalMap ("Normal Map benutzen", Float) = 0
        [Normal][NoScaleOffset] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal-Stärke", Range(0, 2)) = 0.5

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
            half _Painterly;
            half4 _BaseColor;
            half4 _ToneReference;
            half _ToneStrength;
            half4 _Peach;
            half _Saturation;
            half _MidDesaturate;
            half _Wrap;
            half _Bands;
            half _BandSoftness;
            half _BandBlend;
            half4 _TerminatorColor;
            half _TerminatorWidth;
            half _TerminatorStrength;
            half4 _ShadowTint;
            half4 _ShadowWarm;
            half _ShadowStrength;
            half _AmbientStrength;
            half _ShadowLift;
            half _BrushScale;
            half _BrushColor;
            half _BrushLight;
            half _BrushNormal;
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
            half _HighlightSize;
            half _HighlightStrength;
            half4 _RimColor;
            half _RimStrength;
            half _RimPower;
            half _BumpScale;
            float4 _HideMask;
            half _Cutoff;
        CBUFFER_END

        TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BrushMap);     SAMPLER(sampler_BrushMap);
        TEXTURE2D(_VariationMap); SAMPLER(sampler_VariationMap);
        TEXTURE2D(_AOMap);        SAMPLER(sampler_AOMap);
        TEXTURE2D(_BlushMask);    SAMPLER(sampler_BlushMask);
        TEXTURE2D(_ThicknessMap); SAMPLER(sampler_ThicknessMap);
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

            // Pro Pixel gesetzt, in CSShadeLight benutzt
            static half gThin;
            static half gBrush;

            // Gemalte Bänder: Lichtwert in n weiche Stufen, mit glattem Verlauf gemischt
            half PaintedBands(half x)
            {
                half n = max(_Bands, 1.0h);
                half y = x * n;
                half f = frac(y);
                half k = _BandSoftness * 0.5h;
                half stepped = (floor(y) + smoothstep(0.5h - k, 0.5h + k, f)) / n;
                return lerp(x, saturate(stepped), saturate(_BandBlend * _Painterly));
            }

            half3 CSShadeLight(Light light, CSSurface s)
            {
                half NdotL = dot(s.normalWS, light.direction);
                half sh = lerp(1.0h, light.shadowAttenuation, _ShadowStrength);
                sh = lerp(sh, 1.0h, saturate(1.0h - abs(NdotL) * 3.0h) * 0.5h);
                half w = saturate((NdotL + _Wrap) / (1.0h + _Wrap)) * sh;
                // Pinselstriche verschieben die Lichtgrenze => gemalte, unregelmäßige Kante
                w = saturate(w + (gBrush - 0.5h) * _BrushLight * 2.0h * _Painterly * saturate(1.0h - abs(w - 0.5h) * 1.5h));
                half lit = PaintedBands(w);

                // Schatten: warm/kühl gemischt (Pinsel entscheidet), Terminator warm-rosig
                half3 shadowCol = lerp(_ShadowTint.rgb, _ShadowWarm.rgb, lerp(0.5h, gBrush, _Painterly));
                half3 tone = lerp(shadowCol, 1.0h.xxx, lit);
                half term = saturate(1.0h - abs(lit - 0.5h) / max(_TerminatorWidth, 0.01h));
                tone = lerp(tone, tone * _TerminatorColor.rgb * 1.25h, term * _TerminatorStrength);
                half3 col = s.albedo * tone * lit;
                // Mitteltöne leicht entsättigen (malerisch)
                half mid = saturate(1.0h - abs(lit - 0.55h) * 2.5h) * _MidDesaturate * _Painterly;
                half l = dot(col, half3(0.3h, 0.59h, 0.11h));
                col = lerp(col, l.xxx * half3(1.02h, 0.98h, 0.95h), mid);

                // Gemaltes, weiches Glanzlicht
                half3 h = SafeNormalize(light.direction + s.viewDirWS);
                half hl = smoothstep(_HighlightSize, _HighlightSize + 0.06h, dot(s.normalWS, h)) * _HighlightStrength * lit;
                col += hl.xxx;

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

                // Pinselstriche: Höhe + Gefälle (für Impasto-Relief)
                float2 buv = i.uv * _BrushScale;
                float2 du = float2(1.0 / 512.0, 0);
                half b0 = SAMPLE_TEXTURE2D(_BrushMap, sampler_BrushMap, buv).r;
                half bx = SAMPLE_TEXTURE2D(_BrushMap, sampler_BrushMap, buv + du.xy).r;
                half by = SAMPLE_TEXTURE2D(_BrushMap, sampler_BrushMap, buv + du.yx).r;
                gBrush = b0;
                // Farbe: helle Striche wärmer/heller, dunkle leicht kühler (Farbmischung wie Öl)
                half bc = (b0 - 0.5h) * _BrushColor * 2.0h * _Painterly;
                albedo *= 1.0h + bc * half3(1.0h, 0.75h, 0.55h);

                half blush = SAMPLE_TEXTURE2D(_BlushMask, sampler_BlushMask, i.uv).r * _BlushStrength;
                albedo = lerp(albedo, albedo * _BlushColor.rgb * 1.35h, blush);
                half ao = SAMPLE_TEXTURE2D(_AOMap, sampler_AOMap, i.uv).r;
                albedo *= lerp(1.0h.xxx, lerp(_AOColor.rgb, 1.0h.xxx, ao), _AOStrength);

                half lum = dot(albedo, half3(0.3h, 0.59h, 0.11h));
                albedo = saturate(lerp(lum.xxx, albedo, _Saturation));
                gThin = SAMPLE_TEXTURE2D(_ThicknessMap, sampler_ThicknessMap, i.uv).r;

                half3 n = normalize(i.normalWS);
                half3 t = normalize(i.tangentWS.xyz);
                half3 bt = cross(n, t) * i.tangentWS.w;
            #if defined(_NORMALMAP)
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                n = normalize(mul(nTS, half3x3(t, bt, n)));
            #endif
                // Impasto: Pinsel-Gefälle kippt die Normale leicht
                n = normalize(n - (t * (bx - b0) + bt * (by - b0)) * _BrushNormal * _Painterly * 8.0h);
                n = isFrontFace ? n : -n;

                CSSurface s = (CSSurface)0;
                s.positionWS = i.positionWS;
                s.normalWS = n;
                s.viewDirWS = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                s.albedo = albedo;

                half3 color = CSAccumulateLights(s, i.positionCS);
                half3 shadowCol = lerp(_ShadowTint.rgb, _ShadowWarm.rgb, lerp(0.5h, gBrush, _Painterly));
                color += SampleSH(n) * albedo * lerp(shadowCol, 1.0h.xxx, 0.4h) * _AmbientStrength;
                color = max(color, albedo * shadowCol * _ShadowLift);

                half NdotV = saturate(dot(n, s.viewDirWS));
                color += pow(1.0h - NdotV, _RimPower) * _RimStrength * _RimColor.rgb * albedo;

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
