#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

texture SceneTexture;
float Exposure;
float Gamma;
float VignetteStrength;
float2 InverseResolution;

sampler2D SceneSampler = sampler_state
{
    Texture = <SceneTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

float4 PSMain(float4 color : COLOR0, float2 texCoord : TEXCOORD0) : COLOR0
{
    float3 scene = tex2D(SceneSampler, texCoord).rgb;
    scene = 1.0 - exp(-scene * Exposure);
    scene = pow(max(scene, 0.0), 1.0 / max(Gamma, 0.001));
    float2 centered = texCoord * 2.0 - 1.0;
    float vignette = saturate(1.0 - dot(centered, centered) * VignetteStrength);
    return float4(scene * vignette, 1.0) * color;
}

technique PostProcess
{
    pass Pass0
    {
        PixelShader = compile PS_SHADERMODEL PSMain();
    }
}
