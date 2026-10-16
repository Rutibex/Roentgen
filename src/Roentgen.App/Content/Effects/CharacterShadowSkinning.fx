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
float4x4 Bones[48];

struct VertexInput
{
    float4 Position : POSITION0;
    float4 BlendIndices : BLENDINDICES0;
    float4 BlendWeights : BLENDWEIGHT0;
};

struct VertexOutput
{
    float4 Position : SV_POSITION;
    float Depth : TEXCOORD0;
};

float4x4 SkinMatrix(VertexInput input)
{
    return Bones[(int)input.BlendIndices.x] * input.BlendWeights.x
         + Bones[(int)input.BlendIndices.y] * input.BlendWeights.y
         + Bones[(int)input.BlendIndices.z] * input.BlendWeights.z
         + Bones[(int)input.BlendIndices.w] * input.BlendWeights.w;
}

VertexOutput VSMain(VertexInput input)
{
    VertexOutput output;
    float4 skinned = mul(input.Position, SkinMatrix(input));
    output.Position = mul(mul(skinned, World), LightViewProjection);
    output.Depth = (output.Position.z / max(output.Position.w, 0.0001)) * 0.5 + 0.5;
    return output;
}

float4 PSMain(VertexOutput input) : COLOR0
{
    return float4(input.Depth, input.Depth, input.Depth, 1.0);
}

technique CharacterShadowSkinning
{
    pass Pass0
    {
        VertexShader = compile VS_SHADERMODEL VSMain();
        PixelShader = compile PS_SHADERMODEL PSMain();
    }
}
