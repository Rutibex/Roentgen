#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

texture SceneTexture;
texture MaskTexture;
float Intensity;
float Persistence;

sampler2D SceneSampler = sampler_state
{
    Texture = <SceneTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

sampler2D MaskSampler = sampler_state
{
    Texture = <MaskTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

float4 PSMain(float4 color : COLOR0, float2 texCoord : TEXCOORD0) : COLOR0
{
    float4 scene = tex2D(SceneSampler, texCoord);
    float4 mask = tex2D(MaskSampler, texCoord);
    float persistence = saturate(Persistence);
    float phosphor = saturate(mask.g * Intensity) * lerp(0.65, 1.0, persistence);
    float3 diagnostic = float3(0.55, 1.0, 0.85);
    float3 composite = scene.rgb * (1.0 - phosphor * 0.55) + diagnostic * phosphor;
    return float4(composite, scene.a) * color;
}

technique FocusComposite
{
    pass Pass0
    {
        PixelShader = compile PS_SHADERMODEL PSMain();
    }
}
