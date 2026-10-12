// The mist the world ends in.
//
// One flat, transparent layer of it. The environment builder stacks several at different
// heights, which is what makes it read as fog rather than as a grey sheet: low layers catch the
// feet of whatever walks out of the edge, high ones swallow the tree crowns further in.
//
// How thick it is comes from where the pixel is, not from depth. The level runs with the depth
// texture off, so soft particles and depth fog are both unavailable - but the play area is a
// rectangle, and "how far outside the rectangle" is all the gradient needs. Tongues reach in along
// the approach trails, so the night's zombies appear inside the fog rather than out of nothing,
// and one pocket thins it over the survivors' checkpoint so its lights read through.
//
// _EdgeFogTint is global and set by DayNightLighting. Its alpha is how much of it applies, so a
// scene that never sets it (the editor, a test) gets untinted fog rather than black.
Shader "LastOrders/EdgeFog"
{
    Properties
    {
        _Color ("Colour", Color) = (0.74, 0.78, 0.78, 1)
        _ShadeColor ("Shade colour", Color) = (0.60, 0.65, 0.66, 1)
        _NoiseTex ("Noise", 2D) = "gray" {}
        _NoiseScale ("Noise scale (per metre)", Float) = 0.03
        _Scroll ("Scroll A (xy) and B (zw), m/s", Vector) = (0.35, 0.12, -0.2, 0.28)
        _Opacity ("Opacity", Range(0, 1)) = 0.5
        _NoiseStrength ("Noise strength", Range(0, 1)) = 0.6
        _ClearRect ("Clear rect (xMin, zMin, xMax, zMax)", Vector) = (-36, -31, 72, 66)
        _FogStart ("Fog starts (m outside the rect)", Float) = -4
        _FogEnd ("Fog full (m outside the rect)", Float) = 34
        _Solid ("Solid past the end", Range(0, 1)) = 0
        _Tongue0 ("Tongue 0 (from xz, to xz)", Vector) = (0, 0, 0, 0)
        _Tongue1 ("Tongue 1 (from xz, to xz)", Vector) = (0, 0, 0, 0)
        _Tongue2 ("Tongue 2 (from xz, to xz)", Vector) = (0, 0, 0, 0)
        _Tongue3 ("Tongue 3 (from xz, to xz)", Vector) = (0, 0, 0, 0)
        _Tongue4 ("Tongue 4 (from xz, to xz)", Vector) = (0, 0, 0, 0)
        _Tongue5 ("Tongue 5 (from xz, to xz)", Vector) = (0, 0, 0, 0)
        _Tongue6 ("Tongue 6 (from xz, to xz)", Vector) = (0, 0, 0, 0)
        _Tongue7 ("Tongue 7 (from xz, to xz)", Vector) = (0, 0, 0, 0)
        _TongueWidth ("Tongue half width", Float) = 4
        _TongueStrength ("Tongue strength", Range(0, 1)) = 0.6
        _Pocket ("Pocket (x, z, radius, fog kept)", Vector) = (0, 0, 0, 1)
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
            Name "EdgeFog"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _ShadeColor;
                float4 _NoiseTex_ST;
                float _NoiseScale;
                float4 _Scroll;
                half _Opacity;
                half _NoiseStrength;
                float4 _ClearRect;
                float _FogStart;
                float _FogEnd;
                half _Solid;
                float4 _Tongue0;
                float4 _Tongue1;
                float4 _Tongue2;
                float4 _Tongue3;
                float4 _Tongue4;
                float4 _Tongue5;
                float4 _Tongue6;
                float4 _Tongue7;
                float _TongueWidth;
                half _TongueStrength;
                float4 _Pocket;
            CBUFFER_END

            // Global, not per material: one value for every layer, driven by the time of day.
            half4 _EdgeFogTint;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            // Metres outside the clear rectangle; negative inside it.
            float OutsideRect(float2 p)
            {
                float2 centre = (_ClearRect.xy + _ClearRect.zw) * 0.5;
                float2 extent = (_ClearRect.zw - _ClearRect.xy) * 0.5;
                float2 d = abs(p - centre) - extent;
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
            }

            // How much fog a tongue lays at p: full along its spine, easing off toward its tip.
            float Tongue(float2 p, float4 t)
            {
                float2 a = t.xy;
                float2 ab = t.zw - t.xy;
                float len2 = dot(ab, ab);
                if (len2 < 1e-4) return 0.0;

                float h = saturate(dot(p - a, ab) / len2);
                float d = length(p - (a + ab * h));

                float body = 1.0 - smoothstep(_TongueWidth * 0.3, _TongueWidth, d);
                return body * lerp(1.0, 0.55, h);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xz;
                float outside = OutsideRect(p);

                float fog = smoothstep(_FogStart, _FogEnd, outside);

                float tongues = max(max(max(Tongue(p, _Tongue0), Tongue(p, _Tongue1)), max(Tongue(p, _Tongue2), Tongue(p, _Tongue3))),
                                    max(max(Tongue(p, _Tongue4), Tongue(p, _Tongue5)), max(Tongue(p, _Tongue6), Tongue(p, _Tongue7))));
                fog = max(fog, tongues * _TongueStrength);

                // The checkpoint's pocket: thinner, never clear.
                float pocketDistance = length(p - _Pocket.xy);
                float inPocket = 1.0 - smoothstep(_Pocket.z * 0.45, _Pocket.z, pocketDistance);
                fog *= lerp(1.0, _Pocket.w, inPocket * step(0.001, _Pocket.z));

                float2 uv = p * _NoiseScale;
                half n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv + _Scroll.xy * _NoiseScale * _Time.y).r;
                half n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv * 2.3 + 0.37 + _Scroll.zw * _NoiseScale * _Time.y).r;
                half n = saturate(n1 * 0.62 + n2 * 0.38);

                half wisps = lerp(1.0, saturate(n * 1.9 - 0.2), _NoiseStrength);
                half alpha = saturate(fog * wisps * _Opacity * 1.15);

                // Past the far edge the world simply stops, so the top layer closes it off.
                alpha = max(alpha, _Solid * smoothstep(_FogEnd, _FogEnd + 12.0, outside));

                half3 colour = lerp(_ShadeColor.rgb, _Color.rgb, n);
                half3 tint = lerp(half3(1.0, 1.0, 1.0), _EdgeFogTint.rgb, _EdgeFogTint.a);

                return half4(colour * tint, alpha);
            }
            ENDHLSL
        }
    }
}
