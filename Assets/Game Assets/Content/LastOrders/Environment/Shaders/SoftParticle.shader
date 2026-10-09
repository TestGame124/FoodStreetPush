// Particles for small effects in the level: puffs of earth, rings, leaves, sparkles.
//
// A texture tinted by each particle's colour, either blended over what is behind (dust, leaves) or
// added to it (sparkles). Blended particles take the scene's light the way the crop sprites do - a
// share of the sun and of the sky, so a puff of earth darkens at dusk with the soil it came from;
// added ones glow the same by day and by night.
//
// Effects on the ground would be cut in half by it: a camera-facing puff on the soil stands half
// below it. Each vertex is slid toward the camera along its own line of sight, so it lands exactly
// where it was on screen but in front of the soil.
Shader "LastOrders/SoftParticle"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Additive ("Added, not blended (set with the blend modes)", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend", Float) = 10
        _Lit ("Takes the scene's light", Range(0, 1)) = 1
        _DepthPull ("Slide toward the camera (m)", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "SoftParticle"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Additive;
                half _Lit;
                float _DepthPull;
            CBUFFER_END

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
                half3 light : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                half4 colour : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                // Particles arrive in world space already.
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 toCamera = GetCameraPositionWS() - positionWS;
                float distance = length(toCamera);
                positionWS += toCamera * (min(_DepthPull, distance * 0.5) / max(distance, 1e-4));

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.colour = input.colour * _BaseColor;

                Light sun = GetMainLight();
                half3 scene = min(sun.color * 0.6 + SampleSH(half3(0.0, 1.0, 0.0)) * 0.4, 1.15);
                output.light = lerp(half3(1.0, 1.0, 1.0), scene, _Lit);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 colour = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * input.colour;
                colour.rgb *= input.light;
                colour.rgb = MixFog(colour.rgb, input.fogFactor);

                // Added particles carry their own fade: there is no alpha in an additive blend.
                colour.rgb *= lerp(1.0, colour.a, _Additive);
                return colour;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
