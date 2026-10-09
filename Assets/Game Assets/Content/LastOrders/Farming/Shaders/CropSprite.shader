// The crops in the plant pots: sprites rendered from the game camera's angle (CropSprites), turned to
// face the camera (CropSprite), so they read as the smooth toon models they were drawn from.
//
// The shading is already painted into the sprite, as the crop looks under the level's daylight in
// the editor. This only follows the light as it changes: the sprite takes the sun's colour and the
// sky's, weighted so a white sun and a white sky leave it exactly as painted, and darkens where the
// sun's shadow falls - so a crop dims at dusk and in a building's shadow like the props round it.
//
// A sprite facing the camera stands in a plane through the crop's base, tilted back with the view,
// so its lower half - a cabbage's front leaves, the foot of a corn stalk - lies below the soil it
// is drawn over and would be hidden by it. Each vertex is slid toward the camera along its own line
// of sight: exactly where it was on screen, but in front of the soil and the pot's front border.
Shader "LastOrders/CropSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _SunWeight ("Share of the sun", Range(0, 1)) = 0.6
        _SkyWeight ("Share of the sky", Range(0, 1)) = 0.4
        _MaxLight ("Brightest light", Range(1, 2)) = 1.15
        _DepthPull ("Slide toward the camera (m)", Range(0, 1)) = 0.35
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
            Name "CropSprite"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _SunWeight;
                half _SkyWeight;
                half _MaxLight;
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
                float3 positionWS : TEXCOORD1;
                half3 sky : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                half4 colour : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                // World space throughout, so it holds when the renderer batches sprites into one mesh.
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 toCamera = GetCameraPositionWS() - positionWS;
                float distance = length(toCamera);
                positionWS += toCamera * (min(_DepthPull, distance * 0.5) / max(distance, 1e-4));

                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.colour = input.colour * _Color;
                output.sky = SampleSH(half3(0.0, 1.0, 0.0)) * _SkyWeight;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.colour;
                clip(sprite.a - 0.004);

                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shade = lerp(1.0, sun.shadowAttenuation, 0.85);
                half3 light = min(sun.color * shade * _SunWeight + input.sky, _MaxLight);

                half3 colour = MixFog(sprite.rgb * light, input.fogFactor);
                return half4(colour, sprite.a);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
