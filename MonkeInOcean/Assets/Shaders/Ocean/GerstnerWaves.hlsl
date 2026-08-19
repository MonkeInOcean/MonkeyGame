#ifndef OCEAN_GERSTNER_INCLUDED
#define OCEAN_GERSTNER_INCLUDED

// Shared Gerstner wave evaluation. Included by Ocean.shader and mirrored in
// C# (GerstnerSampler.cs) so gameplay buoyancy matches the visible surface.
//
// A wave is packed as float4( dirX, dirY, steepness, wavelength ):
//   dir        - horizontal travel direction (xz plane), normalized internally
//   steepness  - 0..1 crest sharpness (keep the SUM across waves <= 1 to avoid
//                self-intersecting loops / pinching)
//   wavelength - crest-to-crest distance in world units
//
// Waves are evaluated in WORLD space so the pattern stays anchored while the
// ocean grid recenters on the camera (no vertex "swimming").

#ifndef PI
#define PI 3.14159265359
#endif

// Gravity constant tuned for game-scale water (real 9.8 makes long waves race).
#define OCEAN_GRAVITY 9.8

// Accumulates one Gerstner wave onto position + derivative frames.
// Returns the world-space displacement to add to p.
float3 GerstnerWave(
    float4 wave, float3 p, float speed, float time,
    inout float3 tangent, inout float3 binormal, inout float crest)
{
    float steepness = wave.z;
    float wavelength = max(wave.w, 0.0001);
    float k = 2.0 * PI / wavelength;
    float c = sqrt(OCEAN_GRAVITY / k) * speed;
    float2 d = normalize(wave.xy);
    float f = k * (dot(d, p.xz) - c * time);
    float a = steepness / k;

    float sinF = sin(f);
    float cosF = cos(f);

    tangent += float3(
        -d.x * d.x * (steepness * sinF),
         d.x * (steepness * cosF),
        -d.x * d.y * (steepness * sinF));

    binormal += float3(
        -d.x * d.y * (steepness * sinF),
         d.y * (steepness * cosF),
        -d.y * d.y * (steepness * sinF));

    // Crest signal: peaks near wave tops where several waves stack, used to
    // drive whitecap foam. Positive-biased so troughs contribute little.
    crest += steepness * cosF;

    return float3(d.x * (a * cosF), a * sinF, d.y * (a * cosF));
}

// Evaluates the full 4-wave sum. `worldPos` is the flat (rest) world position;
// returns the displaced world position and fills the world-space normal + a
// 0..1 foam factor for crests.
float3 SampleGerstner(
    float3 worldPos, float4 waveA, float4 waveB, float4 waveC, float4 waveD,
    float speed, float time, out float3 normalWS, out float foam)
{
    float3 tangent = float3(1, 0, 0);
    float3 binormal = float3(0, 0, 1);
    float crest = 0;

    float3 p = worldPos;
    p += GerstnerWave(waveA, worldPos, speed, time, tangent, binormal, crest);
    p += GerstnerWave(waveB, worldPos, speed, time, tangent, binormal, crest);
    p += GerstnerWave(waveC, worldPos, speed, time, tangent, binormal, crest);
    p += GerstnerWave(waveD, worldPos, speed, time, tangent, binormal, crest);

    normalWS = normalize(cross(binormal, tangent));
    foam = saturate(crest);
    return p;
}

#endif
