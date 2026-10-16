#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 World;
float4x4 View;
float4x4 WorldInverseTranspose;
float4x4 Projection;
float4x4 LightViewProjection;
float3 CameraPosition;
float3 LightDirection;
float3 LightColor;
float4 AlbedoColor;
float4 FogColor;
float AmbientIntensity;
float EmissiveStrength;
float Roughness;
float Metallic;
float FogStart;
float FogEnd;
float ShadowStrength;
texture AlbedoTexture;
texture ShadowMap;

sampler2D AlbedoSampler = sampler_state
{
    Texture = <AlbedoTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Wrap;
    AddressV = Wrap;
};

sampler2D ShadowSampler = sampler_state
{
    Texture = <ShadowMap>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

struct VertexInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 TexCoord : TEXCOORD0;
    float4 Color : COLOR0;
};

struct VertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 WorldNormal : TEXCOORD1;
    float2 TexCoord : TEXCOORD2;
    float4 Color : COLOR0;
    float4 ShadowPosition : TEXCOORD3;
};

VertexOutput VSMain(VertexInput input)
{
    VertexOutput output;
    float4 worldPosition = mul(input.Position, World);
    output.Position = mul(mul(worldPosition, View), Projection);
    output.WorldPosition = worldPosition.xyz;
    output.WorldNormal = normalize(mul(input.Normal, (float3x3)WorldInverseTranspose));
    output.TexCoord = input.TexCoord;
    output.Color = input.Color;
    output.ShadowPosition = mul(worldPosition, LightViewProjection);
    return output;
}

float SampleShadow(float4 shadowPosition)
{
    float3 projected = shadowPosition.xyz / max(shadowPosition.w, 0.0001);
    float2 uv = projected.xy * float2(0.5, -0.5) + 0.5;
    if (uv.x <= 0.0 || uv.x >= 1.0 || uv.y <= 0.0 || uv.y >= 1.0)
    {
        return 1.0;
    }

    float storedDepth = tex2D(ShadowSampler, uv).r;
    float currentDepth = projected.z * 0.5 + 0.5;
    return currentDepth - 0.0025 <= storedDepth ? 1.0 : 1.0 - ShadowStrength;
}

float4 PSMain(VertexOutput input) : COLOR0
{
    float3 normal = normalize(input.WorldNormal);
    float diffuse = saturate(dot(normal, -normalize(LightDirection)));
    float shadow = SampleShadow(input.ShadowPosition);
    float3 baseColor = tex2D(AlbedoSampler, input.TexCoord).rgb * AlbedoColor.rgb * input.Color.rgb;
    float3 lighting = float3(AmbientIntensity, AmbientIntensity, AmbientIntensity) + LightColor * diffuse * shadow;
    float3 viewDirection = normalize(CameraPosition-input.WorldPosition);
    float3 halfway = normalize(viewDirection-normalize(LightDirection));
    float specular = pow(saturate(dot(normal,halfway)),lerp(96.0,8.0,Roughness)) * shadow;
    float3 reflectance = lerp(float3(0.035,0.035,0.035),baseColor*0.45,Metallic);
    float3 litColor = baseColor * lighting + specular*reflectance*LightColor + baseColor * EmissiveStrength;
    float distanceToCamera = distance(CameraPosition, input.WorldPosition);
    float fogFactor = saturate((FogEnd - distanceToCamera) / max(FogEnd - FogStart, 0.001));
    float3 finalColor = lerp(FogColor.rgb, litColor, fogFactor);
    return float4(finalColor, AlbedoColor.a * input.Color.a);
}

technique ForwardLit
{
    pass Pass0
    {
        VertexShader = compile VS_SHADERMODEL VSMain();
        PixelShader = compile PS_SHADERMODEL PSMain();
    }
}
