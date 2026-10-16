#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 WorldViewProjection;
float4 DiagnosticColor;

struct VertexInput
{
    float4 Position : POSITION0;
};

struct VertexOutput
{
    float4 Position : SV_POSITION;
};

VertexOutput VSMain(VertexInput input)
{
    VertexOutput output;
    output.Position = mul(input.Position, WorldViewProjection);
    return output;
}

float4 PSMain() : COLOR0
{
    return DiagnosticColor;
}

technique FocusDiagnosticMask
{
    pass Pass0
    {
        VertexShader = compile VS_SHADERMODEL VSMain();
        PixelShader = compile PS_SHADERMODEL PSMain();
    }
}
