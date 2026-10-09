// A round, soft-edged spot: the pool a floodlight throws on the ground, the glow round a fire
// barrel, one puff of drifting mist.
//
// Drawn from the quad's UVs rather than a texture, so there is nothing to import and the edge
// never shows a texel. Particle systems feed it their vertex colour.
//
// _NightOnly makes it follow the global _EnvNightGlow (0 by day, 1 at night, set by
// DayNightLighting): a light pool is only worth seeing once the sun has gone. _UseFogTint lets a
// mist puff darken with the rest of the fog.
Shader "LastOrders/SoftBlob"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.85, 0.5, 0.6)
        _Softness ("Softness", Range(0.05, 1)) = 0.8
        _NightOnly ("Only at night", Range(0, 1)) = 0
        _UseFogTint ("Use the fog tint", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "SoftBlob"

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Softness;
                half _NightOnly;
                half _UseFogTint;
            CBUFFER_END

            half4 _EdgeFogTint;
            half _EnvNightGlow;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 colour : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 colour : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.colour = input.colour;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float r = length(input.uv * 2.0 - 1.0);
                half a = 1.0 - smoothstep(1.0 - _Softness, 1.0, r);

                half4 colour = _Color * input.colour;
                colour.a *= a * lerp(1.0, _EnvNightGlow, _NightOnly);

                half3 tint = lerp(half3(1.0, 1.0, 1.0), _EdgeFogTint.rgb, _EdgeFogTint.a * _UseFogTint);
                colour.rgb *= tint;

                return colour;
            }
            ENDHLSL
        }
    }
}
