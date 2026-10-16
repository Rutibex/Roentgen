#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 World;
float4x4 LightViewProjection;

struct VertexInput
{
    float4 Position : POSITION0;
};

struct VertexOutput
{
    float4 Position : SV_POSITION;
    float Depth : TEXCOORD0;
};

VertexOutput VSMain(VertexInput input)
{
    VertexOutput output;
    output.Position = mul(mul(input.Position, World), LightViewProjection);
    output.Depth = (output.Position.z / max(output.Position.w, 0.0001)) * 0.5 + 0.5;
    return output;
}

float4 PSMain(VertexOutput input) : COLOR0
{
    return float4(input.Depth, input.Depth, input.Depth, 1.0);
}

technique ShadowDepth
{
    pass Pass0
    {
        VertexShader = compile VS_SHADERMODEL VSMain();
        PixelShader = compile PS_SHADERMODEL PSMain();
    }
}
