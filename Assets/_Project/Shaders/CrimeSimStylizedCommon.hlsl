// CrimeSim – gemeinsame Funktionen für die Stil-Shader (URP 17 / Unity 6)
// Erwartet, dass vorher Core.hlsl und Lighting.hlsl eingebunden wurden.
#ifndef CRIMESIM_STYLIZED_COMMON_INCLUDED
#define CRIMESIM_STYLIZED_COMMON_INCLUDED

struct CSAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct CSVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS   : TEXCOORD2;
    float4 tangentWS  : TEXCOORD3;
    float  fogFactor  : TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

CSVaryings CSForwardVert(CSAttributes v)
{
    CSVaryings o = (CSVaryings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
    VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
    o.positionCS = p.positionCS;
    o.positionWS = p.positionWS;
    o.normalWS = n.normalWS;
    real sign = v.tangentOS.w * GetOddNegativeScale();
    o.tangentWS = float4(n.tangentWS, sign);
    o.uv = v.uv;
    o.fogFactor = ComputeFogFactor(p.positionCS.z);
    return o;
}

// Weiche, "gemalte" Lichtantwort: Wrap-Diffuse + weiche Stufe + warmer Übergang (Fake-SSS)
// wrap 0..1, rampCenter/rampSoft steuern die Schattengrenze, rampBlend 0 = rein weich, 1 = Stufe
half3 CSSoftDiffuse(half NdotL, half wrap, half rampCenter, half rampSoft, half rampBlend, half3 sssColor, half sssStrength)
{
    half w = saturate((NdotL + wrap) / (1.0h + wrap));
    half ramp = smoothstep(rampCenter - rampSoft, rampCenter + rampSoft, w);
    half lit = lerp(w, ramp, rampBlend);
    // Am Terminator (w > 0, lit klein) warm einfärben
    half terminator = saturate(w * (1.0h - lit) * 2.0h);
    return lit.xxx + sssColor * sssStrength * terminator;
}

// Oberflächendaten, die jeder Stil-Shader für CSShadeLight füllt
struct CSSurface
{
    float3 positionWS;
    half3  normalWS;
    half3  viewDirWS;
    half3  strandWS;   // nur Haar: Strähnenrichtung
    half3  albedo;
};
#endif
