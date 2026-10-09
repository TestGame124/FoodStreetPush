// The level's toon shading: trees, bushes and grass, and the diner's furniture.
//
// The art pack's trees are good low-poly shapes coloured from one palette texture, lit like any
// other surface - so every facet shades smoothly into the next and the canopy reads as a grey-green
// lump. This lights them the way a toon game does: each facet either lit or in shade, with a
// narrow eased edge between, the shade cooler rather than just darker, and a soft rim of light on
// the edges facing away from the camera's side. On smooth rounded models - the tables and chairs -
// the same edge becomes a clean cel-shading line across the curve.
//
// With _LeafStrength above zero, over the foliage - only where the colour is green, so trunks,
// dead wood and furniture stay plain -
// it lays a painted leaf pattern, projected from the three world axes so it needs no UVs of its
// own (the pack's UVs all point at single palette swatches), and a gradient up the tree: darker
// low in the canopy where the light does not reach, lighter at the crown.
//
// The height is world height, not the object's: the environment marks its trees for static
// batching, which bakes them into world space and loses each one's own origin. They all stand on
// the ground, which is at zero.
Shader "LastOrders/Toon"
{
    Properties
    {
        _BaseMap ("Palette", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _LeafTex ("Leaf pattern (grey, 0.5 = no change)", 2D) = "gray" {}
        _LeafScale ("Leaf pattern tiles per metre", Float) = 0.28
        _LeafStrength ("Leaf pattern strength", Range(0, 1)) = 0.75
        _ShadeColor ("Shade (multiplies the light in shadow)", Color) = (0.42, 0.5, 0.62, 1)
        _Threshold ("Light threshold (N.L)", Range(-1, 1)) = 0.05
        _Softness ("Edge softness", Range(0.001, 0.5)) = 0.07
        _LightStrength ("Direct light", Range(0, 2)) = 0.9
        _AmbientStrength ("Ambient", Range(0, 2)) = 1
        _CanopyLow ("Low canopy brightness", Range(0, 1.5)) = 0.78
        _CanopyHigh ("Crown brightness", Range(0, 1.5)) = 1.15
        _CanopyHeight ("Crown height (m)", Float) = 7
        _RimColor ("Rim light", Color) = (1, 0.97, 0.85, 1)
        _RimStrength ("Rim strength", Range(0, 1)) = 0.18
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_LeafTex);
        SAMPLER(sampler_LeafTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float4 _LeafTex_ST;
            float _LeafScale;
            half _LeafStrength;
            half4 _ShadeColor;
            half _Threshold;
            half _Softness;
            half _LightStrength;
            half _AmbientStrength;
            half _CanopyLow;
            half _CanopyHigh;
            float _CanopyHeight;
            half4 _RimColor;
            half _RimStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half Leaves(float3 positionWS, half3 normalWS)
            {
                half3 w = pow(abs(normalWS), 4.0);
                w /= max(w.x + w.y + w.z, 1e-4);

                float3 p = positionWS * _LeafScale;
                half x = SAMPLE_TEXTURE2D(_LeafTex, sampler_LeafTex, p.zy).r;
                half y = SAMPLE_TEXTURE2D(_LeafTex, sampler_LeafTex, p.xz + 0.37).r;
                half z = SAMPLE_TEXTURE2D(_LeafTex, sampler_LeafTex, p.xy + 0.71).r;
                return x * w.x + y * w.y + z * w.z;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half3 normalWS = normalize(input.normalWS);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;

                // Foliage is whatever the palette paints green.
                half foliage = saturate((albedo.g - max(albedo.r, albedo.b)) * 12.0);

                half leaf = Leaves(input.positionWS, normalWS);
                albedo *= lerp(1.0, leaf * 2.0, foliage * _LeafStrength);

                half height = saturate(input.positionWS.y / _CanopyHeight);
                albedo *= lerp(1.0, lerp(_CanopyLow, _CanopyHigh, height), foliage);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half ndl = dot(normalWS, mainLight.direction);
                half lit = smoothstep(_Threshold - _Softness, _Threshold + _Softness, ndl);
                lit *= lerp(1.0, mainLight.shadowAttenuation, 0.85);

                half3 band = lerp(_ShadeColor.rgb, half3(1.0, 1.0, 1.0), lit);
                half3 light = mainLight.color * band * _LightStrength + SampleSH(normalWS) * _AmbientStrength;

                half3 viewWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(1.0 - saturate(dot(viewWS, normalWS)), 3.0) * _RimStrength * (0.35 + 0.65 * lit);

                half3 colour = albedo * light + _RimColor.rgb * rim * albedo;
                colour = MixFog(colour, input.fogFactor);
                return half4(colour, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirection = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirection = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings NormalsVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 NormalsFrag(Varyings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
