// Paints the crop models that CropSprites renders into the plant pots' sprites. Editor only: it is
// never on anything in the level, so builds leave it out.
//
// It lights a crop the way the level's toon shader lights a prop - lit or in a cool shade, with a
// wide eased edge between so the curve of a leaf reads as a gradient rather than a cel line - and
// adds what a small painted crop needs: a colour running from a leaf's base to its tip, a paler
// midrib, a darker edge where one leaf lies over another, grooves between corn kernels, a toon
// highlight on fruit, and a darkening near the soil.
//
// It is drawn from a command buffer, not a camera, so no render pipeline, scene light or post
// effect is involved: the matrix, the light and the view come in as globals (_CropBake*), set by
// CropSprites. The mesh is already in world space round the crop's base, so there is no object
// matrix. Nothing below the soil (y < 0) is drawn.
Shader "Hidden/LastOrders/CropBake"
{
    Properties
    {
        _ColorA ("Base colour", Color) = (0.3, 0.5, 0.2, 1)
        _ColorB ("Tip colour", Color) = (0.6, 0.8, 0.3, 1)
        _GradientPower ("Gradient curve (base to tip)", Range(0.2, 4)) = 1
        _Vein ("Midrib", Range(0, 1)) = 0
        _EdgeShade ("Leaf edge darkening", Range(0, 1)) = 0
        _Kernels ("Kernels (columns, rows, depth, 0)", Vector) = (0, 0, 0, 0)
        _ShadeColor ("Shade", Color) = (0.56, 0.62, 0.78, 1)
        _Threshold ("Light threshold (N.L)", Range(-1, 1)) = -0.05
        _Softness ("Edge softness", Range(0.01, 1)) = 0.32
        _Rim ("Rim light", Range(0, 1)) = 0.22
        _Gloss ("Highlight", Range(0, 1)) = 0
        _GroundShade ("Brightness at the soil", Range(0, 1)) = 0.7
        _GroundHeight ("Height the soil's shade reaches (m)", Float) = 0.12
    }

    SubShader
    {
        Pass
        {
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4x4 _CropBakeMatrix;
            float3 _CropBakeLight;
            float3 _CropBakeView;

            float4 _ColorA;
            float4 _ColorB;
            float _GradientPower;
            float _Vein;
            float _EdgeShade;
            float4 _Kernels;
            float4 _ShadeColor;
            float _Threshold;
            float _Softness;
            float _Rim;
            float _Gloss;
            float _GroundShade;
            float _GroundHeight;

            struct Attributes
            {
                float3 position : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normal : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 position : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = mul(_CropBakeMatrix, float4(input.position, 1.0));
                output.normal = input.normal;
                output.uv = input.uv;
                output.position = input.position;
                return output;
            }

            float4 Frag(Varyings input, bool front : SV_IsFrontFace) : SV_Target
            {
                clip(input.position.y + 0.002);

                // Leaves are single sheets: the side facing the camera is the one lit. (Drawing into a
                // texture flips the picture, but Unity turns the culling round with it, so "front"
                // still means facing the camera.)
                float3 n = normalize(input.normal) * (front ? 1.0 : -1.0);
                float3 l = normalize(_CropBakeLight);
                float3 v = normalize(_CropBakeView);

                float3 albedo = lerp(_ColorA.rgb, _ColorB.rgb, pow(saturate(input.uv.y), _GradientPower));

                float rib = 1.0 - smoothstep(0.015, 0.07, abs(input.uv.x - 0.5));
                albedo = lerp(albedo, albedo * 1.18 + 0.06, rib * _Vein * smoothstep(0.0, 0.15, input.uv.y) * (1.0 - smoothstep(0.75, 1.0, input.uv.y)));

                albedo *= 1.0 - smoothstep(0.72, 1.0, abs(input.uv.x - 0.5) * 2.0) * _EdgeShade;

                if (_Kernels.z > 0.0)
                {
                    // Every other row shifted half a kernel, as they grow on the cob.
                    float2 g = input.uv * _Kernels.xy;
                    g.x += 0.5 * fmod(floor(g.y), 2.0);
                    float2 cell = abs(frac(g) - 0.5) * 2.0;
                    float groove = smoothstep(0.62, 0.95, max(cell.x, cell.y));
                    albedo *= 1.0 - groove * _Kernels.z;
                }

                float ndl = dot(n, l);
                float lit = smoothstep(_Threshold - _Softness, _Threshold + _Softness, ndl);
                float3 band = lerp(_ShadeColor.rgb, float3(1.0, 1.0, 1.0), lit);
                // A little more light still toward the part turned full to the sun: the gradient in the gradient.
                band *= lerp(0.94, 1.06, saturate(ndl));

                float ground = lerp(_GroundShade, 1.0, smoothstep(0.0, _GroundHeight, input.position.y));

                float facing = saturate(dot(n, v));
                float rim = pow(1.0 - facing, 3.0) * _Rim * (0.4 + 0.6 * lit);

                float3 h = normalize(l + v);
                float gloss = smoothstep(0.93, 0.965, dot(n, h)) * _Gloss;

                float3 colour = albedo * band * ground + rim * (albedo * 0.7 + 0.2) + gloss * 0.55;
                return float4(saturate(colour), 1.0);
            }
            ENDHLSL
        }
    }
}
