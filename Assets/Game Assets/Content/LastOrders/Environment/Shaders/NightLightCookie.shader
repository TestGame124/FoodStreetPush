// Paints the night lights into the sun's cookie (LastOrders.Lighting.NightLights).
//
// The cookie is laid over the sun's own right and up axes, so a light on the ground is drawn where
// the sun's world-to-light matrix puts that patch of ground: a lamp's round pool lands in the cookie
// as the slanted ellipse the sun sees it as, and URP's lookup brings it back round on the ground.
//
// Three passes into one half-float target cleared to black:
//   0  Fill  - lit areas, blended with MAX so where the compound and a claimed plot overlap, or the
//              restaurant's room sits in the yard, the brighter wins instead of the two adding up.
//   1  Lamp  - lamp pools (and areas that are not fill), ADDED.
//   2  Base  - the sun's own light: 1, or the drifting cloud shade fading out with the dusk. Added
//              last, so the cookie is   base + lights   and the lights are scaled to the sun already.
//
// Only drawn when something changes - a light moves, the sun turns or dims - never per frame at night.
Shader "Hidden/LastOrders/NightLightCookie"
{
    Properties
    {
        _CloudTex ("Clouds", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        ZWrite Off
        ZTest Always
        Cull Off
        ColorMask RGB

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Shared by every draw: set on the material before the buffer runs.
        float4x4 _NightWorldToLight;
        float4 _NightCookieRect;     // xy: the cookie's corner in the sun's right/up metres, zw: 1 / its size
        float _NightGround;          // world height of the ground the lights fall on

        // Per light.
        float4 _Centre;              // xy: world X, Z of the middle; zw: the light's own X axis on the ground
        float4 _Shape;               // xy: half size (a round pool's radius twice), z: fade past the edge, w: 1 round, 0 area
        float4 _Colour;              // rgb: light, already divided by the sun's
        float4 _Pools;               // xy: ceiling-light spacing along the area's X and Z (0 for none), z: strength

        // Base.
        TEXTURE2D(_CloudTex);
        SAMPLER(sampler_CloudTex);
        float4 _CloudUV;             // xy: the clouds' offset, z: 1 / tile metres, w: how much of their shade is left

        struct Attributes
        {
            float4 positionOS : POSITION;   // a quad, corners at -1 and 1
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 local : TEXCOORD0;
        };

        // The cookie texture's 0..1 across the given light-space point, written so that sampling the
        // target at that uv finds it on every graphics API.
        float4 CookieClip(float2 cookieUV)
        {
            float4 clip = float4(cookieUV * 2.0 - 1.0, 0.5, 1.0);
            #if UNITY_UV_STARTS_AT_TOP
            clip.y = -clip.y;
            #endif
            return clip;
        }

        Varyings VertLight(Attributes input)
        {
            Varyings output;

            float2 reach = _Shape.xy + (1.0 - _Shape.w) * _Shape.z;
            float2 local = input.positionOS.xy * reach;

            float2 axisX = _Centre.zw;
            float2 axisZ = float2(-axisX.y, axisX.x);
            float2 world = _Centre.xy + axisX * local.x + axisZ * local.y;

            float2 lightSpace = mul(_NightWorldToLight, float4(world.x, _NightGround, world.y, 1.0)).xy;
            output.positionCS = CookieClip((lightSpace - _NightCookieRect.xy) * _NightCookieRect.zw);
            output.local = local;
            return output;
        }

        half4 FragLight(Varyings input) : SV_Target
        {
            half f;

            if (_Shape.w > 0.5)
            {
                // A lamp's pool: bright under it, easing to nothing at the radius with no edge to see.
                float d = length(input.local) / _Shape.x;
                f = saturate(1.0 - d * d);
                f *= f;
            }
            else
            {
                // A lit area: full inside, fading out over the edge distance - light spilling over a wall.
                float2 q = abs(input.local) - _Shape.xy;
                float outside = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);
                f = 1.0 - smoothstep(-0.25 * _Shape.z, _Shape.z, outside);

                // Ceiling lights: a soft brighter pool under each, laid wall to wall.
                if (_Pools.x > 0.0)
                {
                    float2 cell = frac((input.local + _Shape.xy) / _Pools.xy) - 0.5;
                    float r = length(cell * _Pools.xy) / (0.5 * min(_Pools.x, _Pools.y));
                    half pool = saturate(1.0 - r * r);
                    pool *= pool;
                    f *= 1.0 + _Pools.z * (pool - 0.3);
                }
            }

            return half4(_Colour.rgb * f, 0.0);
        }

        Varyings VertBase(Attributes input)
        {
            Varyings output;
            output.local = input.positionOS.xy * 0.5 + 0.5;
            output.positionCS = CookieClip(output.local);
            return output;
        }

        half4 FragBase(Varyings input) : SV_Target
        {
            if (_CloudUV.w <= 0.0) return half4(1.0, 1.0, 1.0, 0.0);

            float2 lightSpace = _NightCookieRect.xy + input.local / _NightCookieRect.zw;
            half3 shade = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, (lightSpace - _CloudUV.xy) * _CloudUV.z + 0.5).rgb;
            return half4(lerp(half3(1.0, 1.0, 1.0), shade, _CloudUV.w), 0.0);
        }
        ENDHLSL

        Pass
        {
            Name "Fill"
            BlendOp Max
            Blend One One

            HLSLPROGRAM
            #pragma vertex VertLight
            #pragma fragment FragLight
            ENDHLSL
        }

        Pass
        {
            Name "Lamp"
            Blend One One

            HLSLPROGRAM
            #pragma vertex VertLight
            #pragma fragment FragLight
            ENDHLSL
        }

        Pass
        {
            Name "Base"
            Blend One One

            HLSLPROGRAM
            #pragma vertex VertBase
            #pragma fragment FragBase
            ENDHLSL
        }
    }
}
