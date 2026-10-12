// The sun's cookie, looked up where a vertex shader may: at an explicit mip level. URP's own
// SampleMainLightCookie samples with screen derivatives, which only a fragment shader has.
//
// For the level's vertex-lit sprites and particles, so dust and smoke kicked up in the lit yard at
// night (LastOrders.Lighting.NightLights paints the lamps into this cookie) are lit like the ground
// under them, and dimmed by the clouds by day. Needs `#pragma multi_compile_vertex _ _LIGHT_COOKIES`
// and URP's Lighting.hlsl included first.
#ifndef LASTORDERS_MAIN_LIGHT_COOKIE_VERTEX_INCLUDED
#define LASTORDERS_MAIN_LIGHT_COOKIE_VERTEX_INCLUDED

half3 MainLightCookieVertex(float3 positionWS)
{
#if defined(_LIGHT_COOKIES)
    if (!IsMainLightCookieEnabled()) return half3(1.0, 1.0, 1.0);

    float2 uv = ComputeLightCookieUVDirectional(_MainLightWorldToLight, positionWS, float4(1, 1, 0, 0), URP_TEXTURE_WRAP_MODE_NONE);
    half4 cookie = SAMPLE_TEXTURE2D_LOD(_MainLightCookieTexture, sampler_MainLightCookieTexture, uv, 0);

    return IsMainLightCookieTextureRGBFormat() ? cookie.rgb
         : IsMainLightCookieTextureAlphaFormat() ? cookie.aaa
         : cookie.rrr;
#else
    return half3(1.0, 1.0, 1.0);
#endif
}

#endif
