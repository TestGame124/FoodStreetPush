// The level's wind, for any shader that moves with it: trees and bushes first.
//
// AmbientWind (Scripts/LastOrders/Ambience) sets the two globals every frame. A level without one
// leaves them at zero, and then nothing moves: the strength is zero and every offset with it.
//
// Gusts are not a number that rises and falls everywhere at once. Each one is a front that rolls
// across the level downwind, so the forest leans in a wave, tree after tree. The envelope is three
// slow sines eased into separate swells - AmbientWind.GustAt is the same sum, so the leaves the
// particles throw and the birds that ride it feel the same gust the trees show.
#ifndef LASTORDERS_AMBIENT_WIND_INCLUDED
#define LASTORDERS_AMBIENT_WIND_INCLUDED

// xy: the heading the wind blows toward (unit, world x and z). z: the steady breeze, 0 to 1.
// w: how much a gust adds on top.
float4 _AmbWind;

// x: the wind's clock in seconds. y: how fast a gust front travels, metres a second.
float4 _AmbWindClock;

float AmbGustAt(float x)
{
    float n = sin(x * 0.37) * 0.5 + sin(x * 0.23 + 1.7) * 0.35 + sin(x * 0.61 + 4.1) * 0.15;
    return smoothstep(0.2, 0.8, n);
}

// 0 to about 1: the breeze plus whatever gust is passing this point now.
float AmbWindStrength(float3 positionWS)
{
    float along = dot(positionWS.xz, _AmbWind.xy);
    float gust = AmbGustAt(_AmbWindClock.x - along / max(_AmbWindClock.y, 0.1));
    return _AmbWind.z + _AmbWind.w * gust;
}

// How far a point of a plant moves: bent downwind up to `bend` metres at full strength and swaying
// about that lean, with `flutter` metres of quick leaf shiver on top.
float3 AmbSway(float3 positionWS, float bend, float flutter)
{
    float strength = AmbWindStrength(positionWS);
    float t = _AmbWindClock.x;
    float3 heading = float3(_AmbWind.x, 0.0, _AmbWind.y);
    float3 across = float3(-heading.z, 0.0, heading.x);

    // Each tree a little out of step with its neighbours, so the forest moves as a field rather
    // than in unison - the phase drifts across the ground, and every point of one tree shares it.
    float phase = t * 1.6 + dot(positionWS.xz, float2(0.13, 0.17));
    float sway = strength * (0.55 + 0.45 * sin(phase)) + 0.12 * strength * sin(phase * 2.3 + 1.1);

    float3 offset = heading * sway * bend;
    offset += across * sin(phase * 0.8 + 0.6) * 0.22 * strength * bend;

    // A leaning stem is shorter: the crown drops a touch as it goes over.
    offset.y -= sway * sway * bend * 0.15;

    float shiver = sin(t * 9.0 + dot(positionWS, float3(4.1, 3.3, 3.7))) * (0.25 + strength);
    offset += float3(0.6, 0.4, -0.5) * shiver * flutter;
    return offset;
}

#endif
