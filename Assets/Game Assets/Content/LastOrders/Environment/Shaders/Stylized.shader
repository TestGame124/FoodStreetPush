// Smooth low-poly stylised shading, for props modelled in code: the plant beds first.
//
// The colour is painted into the mesh, one colour per vertex: the shading a painter would put on
// a hand-painted model - darker where a board meets the ground or the soil, a worn light edge
// along the top, cavities in the furrows - so the model needs no texture or UVs and every piece of
// a prop shares one material.
//
// The light wraps round the form and fades through a broad soft gradient into a cooler shade, with
// no band or terminator line anywhere: the matte, rounded look of the game's characters, not a toon
// cel. Main light and sky only, as on the level's toon shader, so the two sit together by day and by
// night.
//
// _BaseColor tints the lot, which is also how the building ghost shows green or red.
Shader "LastOrders/Stylized"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade (multiplies the light away from the sun)", Color) = (0.55, 0.6, 0.74, 1)
        _Wrap ("Light wrap (how far round the form the light reaches)", Range(0, 1)) = 0.55
        _LightStrength ("Direct light", Range(0, 2)) = 0.9
        _AmbientStrength ("Ambient", Range(0, 2)) = 0.62
        _ShadowStrength ("Cast shadow strength", Range(0, 1)) = 0.8
        _RimColor ("Rim light", Color) = (1, 0.96, 0.86, 1)
        _RimStrength ("Rim strength", Range(0, 1)) = 0.08
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

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ShadeColor;
            half _Wrap;
            half _LightStrength;
            half _AmbientStrength;
            half _ShadowStrength;
            half4 _RimColor;
            half _RimStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "StylizedForward"
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
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
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
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half3 normalWS = normalize(input.normalWS);
                half3 albedo = input.color.rgb * _BaseColor.rgb;

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                // Wrapped, then eased: lit faces stay lit a long way round the form and the shade
                // arrives as a slow gradient rather than at a line.
                half wrapped = saturate((dot(normalWS, mainLight.direction) + _Wrap) / (1.0 + _Wrap));
                half lit = wrapped * wrapped * (3.0 - 2.0 * wrapped);
                lit *= lerp(1.0, mainLight.shadowAttenuation, _ShadowStrength);

                half3 direct = mainLight.color * lerp(_ShadeColor.rgb, half3(1.0, 1.0, 1.0), lit) * _LightStrength;
                half3 light = direct + SampleSH(normalWS) * _AmbientStrength;

                half3 viewWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(1.0 - saturate(dot(viewWS, normalWS)), 3.0) * _RimStrength * (0.4 + 0.6 * lit);

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
