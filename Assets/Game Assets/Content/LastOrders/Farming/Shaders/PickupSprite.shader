// The items thrown out into the level as they are made - a crop picked, a dish cooked - and the glow
// behind them (ItemReceiver). Their icons are finished pictures with their own shading and outline,
// so this mostly shows them as they are: a pickup should read at a glance by day and by night. Only a
// small share of the scene's light comes through, so they still sit in the level's colour at dusk.
//
// _Flash whitens the sprite (keeping its shape): the blink as a pickup appears. Set per renderer.
//
// Pickups bounce across plant beds and fly over buildings, so each vertex is slid toward the camera
// along its own line of sight: exactly where it was on screen, but in front of the crops and borders
// it would otherwise dip behind.
Shader "LastOrders/PickupSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Flash ("Flash to white", Range(0, 1)) = 0
        _Lit ("Takes the scene's light", Range(0, 1)) = 0.15
        _Additive ("Added, not blended (set with the blend modes)", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend", Float) = 10
        _DepthPull ("Slide toward the camera (m)", Range(0, 3)) = 1.2
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
            "CanUseSpriteAtlas" = "True"
        }

        Pass
        {
            Name "PickupSprite"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            // The sun's cookie: cloud shade by day, the night lights in the safe zone after dark.
            #pragma multi_compile_vertex _ _LIGHT_COOKIES

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "../../Environment/Shaders/MainLightCookieVertex.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Flash;
                half _Lit;
                half _Additive;
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

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 toCamera = GetCameraPositionWS() - positionWS;
                float distance = length(toCamera);
                positionWS += toCamera * (min(_DepthPull, distance * 0.5) / max(distance, 1e-4));

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.colour = input.colour * _Color;

                Light sun = GetMainLight();
                sun.color *= MainLightCookieVertex(positionWS);
                half3 scene = min(sun.color * 0.6 + SampleSH(half3(0.0, 1.0, 0.0)) * 0.4, 1.15);
                output.light = lerp(half3(1.0, 1.0, 1.0), scene, _Lit);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 colour = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.colour;
                clip(colour.a - 0.004);

                colour.rgb = lerp(colour.rgb * input.light, half3(1.0, 1.0, 1.0), _Flash);
                colour.rgb = MixFog(colour.rgb, input.fogFactor);

                // Added glows carry their own fade: there is no alpha in an additive blend.
                colour.rgb *= lerp(1.0, colour.a, _Additive);
                return colour;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
