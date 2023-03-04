//------------------------------------------------------------------------------
// Fog
// see: Real-time Atmospheric Effects in Games (Carsten Wenzel)
//------------------------------------------------------------------------------

float inScattering(highp vec3 rayStart, vec3 rayDir, highp vec3 lightPos, highp float rayDistance) {
    highp vec3 q = rayStart - lightPos;
    highp float b = dot(rayDir, q);
    highp float c = dot(q, q);
    highp float s = 1.0 / sqrt(c - b*b);

    highp float x = s * rayDistance;
    highp float y = s * b;
    return s * atan(x, 1.0 + (x+y) * y);
}

vec4 fog(vec4 color, highp vec3 view) {
    // note: d can be +inf with the skybox
    highp float d = length(view);

    // early exit for object "in front" of the fog
    if (d < frameUniforms.fogStart) {
        return color;
    }

    // fogCutOffDistance is set to +inf to disable the cutoff distance
    if (d > frameUniforms.fogCutOffDistance) {
        return color;
    }

    // .x = density
    // .y = -fallof*(y-height)
    // .z = density * exp(-fallof*(y-height))
    highp vec3 density = frameUniforms.fogDensity;

    // height falloff [1/m]
    highp float falloff = frameUniforms.fogHeightFalloff;

    // Compute the fog's optical path (unitless) at a distance of 1m at a given height.
    highp float fogOpticalPathAtOneMeter = density.z;
    highp float fh = falloff * view.y;
    if (abs(fh) > 0.00125) {
        // The function below is continuous at fh=0, so to avoid a divide-by-zero, we just clamp fh
        fogOpticalPathAtOneMeter = (density.z - density.x * exp(density.y - fh)) / fh;
    }

    // Compute the integral of the fog density at a given height from fogStart to the fragment
    highp float fogOpticalPath = fogOpticalPathAtOneMeter * max(d - frameUniforms.fogStart, 0.0);

    // Compute the transmittance [0,1] using the Beer-Lambert Law
    float fogTransmittance = exp(-fogOpticalPath);

    // Compute the opacity from the transmittance
    float fogOpacity = min(1.0 - fogTransmittance, frameUniforms.fogMaxOpacity);

    // compute fog color
    vec3 fogColor = frameUniforms.fogColor;

#if MATERIAL_FEATURE_LEVEL > 0
    if (frameUniforms.fogColorFromIbl > 0.0) {
        float normalizedDepth = d * frameUniforms.fogOneOverFarMinusNear - frameUniforms.fogNearOverFarMinusNear;
        lowp vec2 minMaxMip = unpackHalf2x16(frameUniforms.fogMinMaxMip);
        lowp float lod = mix(minMaxMip.y, minMaxMip.x, saturate(normalizedDepth));

        // when sampling the IBL we need to take into account the IBL transform. We know it's a
        // a rigid transform, so we can take the transpose instead of the inverse, and for the
        // same reason we can use it directly instead of taking the cof() to transform a vector.
        highp mat3 worldFromUserWorldMatrix = transpose(mat3(frameUniforms.userWorldFromWorldMatrix));
        fogColor *= textureLod(sampler0_fog, worldFromUserWorldMatrix * view, lod).rgb;
    }
#endif

    fogColor *= frameUniforms.iblLuminance * fogOpacity;

    if (frameUniforms.fogInscatteringSize > 0.0) {
        // compute a new line-integral for a different start distance
        highp float sunOpticalPath =
                fogOpticalPathAtOneMeter * max(d - frameUniforms.fogInscatteringStart, 0.0);

        // Compute the transmittance using the Beer-Lambert Law
        float sunTransmittance = exp(-sunOpticalPath);

        // Add sun colored fog when looking towards the sun
        vec3 sunColor = frameUniforms.lightColorIntensity.rgb * frameUniforms.lightColorIntensity.w;

        float sunAmount = max(dot(normalize(view), frameUniforms.lightDirection), 0.0); // between 0 and 1
        float sunInscattering = pow(sunAmount, frameUniforms.fogInscatteringSize);

        fogColor += sunColor * (sunInscattering * (1.0 - sunTransmittance));
    }
#if defined(VARIANT_HAS_DYNAMIC_LIGHTING) && !defined(MATERIAL_HAS_SHADOW_MULTIPLIER)
    if (true)
    {
        uint channels = object_uniforms_flagsChannels & 0xFFu;
        // Iterate point lights
        for (uint i=0; i<frameUniforms.lightCount; i++) {
            Light light = getLight(i);
            if ((light.channels & channels) == 0u) {
                continue;
            }

            if (light.type == LIGHT_TYPE_SPOT && light.spotCosOuterSquared>0.5 ) {
                highp vec3 D = normalize(view);
                highp vec3 V = light.direction;
                highp vec3 O = getWorldPosition();
                highp vec3 C = light.worldPosition;
                highp float c2 = light.spotCosOuterSquared;

                highp vec3 CO = O - C;
                highp float DV = dot(D, V);
                highp float COV = dot(CO, V);
                highp float a = DV * DV - c2;
                highp float b = 2.0 * (DV * COV - dot(D, CO) * c2);
                highp float c = COV*COV - dot(CO, CO) * c2;

                highp float delta = b*b - 4*a*c;
                if (delta <= 0.0) {
                    continue;
                }

                highp float t0 = (-b - sqrt(delta)) / 2.0 * a;
                highp float t1 = (-b + sqrt(delta)) / 2.0 * a;

                if (t0>0 && t1>0) {
                    continue;
                }

                highp vec3 P0 = O + D * t0;
                highp vec3 P1 = O + D * t1;

                if (dot(P0-C, V) < 0) {
                    continue;
                }

                if (dot(P1-C, V) < 0) {
                    continue;
                }


                // compute a new line-integral for a different start distance
                highp float inscatteringDensityIntegral = fogOpticalPathAtOneMeter * d;
                // Compute the transmittance using the Beer-Lambert Law
                float inscatteringDensity = exp(-inscatteringDensityIntegral);
                // Compute the opacity from the transmittance
                float inscatteringOpacity = 1.0 - inscatteringDensity;

                float inscat = inScattering(
                P0,
                shading_view,
                light.worldPosition, length(P1-P0));

                fogColor += light.colorIntensity.rgb * light.colorIntensity.w  * (inscat * inscatteringOpacity) *0.001;

            } else {

                // compute a new line-integral for a different start distance
                highp float inscatteringDensityIntegral = fogOpticalPathAtOneMeter * d;
                // Compute the transmittance using the Beer-Lambert Law
                float inscatteringDensity = exp(-inscatteringDensityIntegral);
                // Compute the opacity from the transmittance
                float inscatteringOpacity = 1.0 - inscatteringDensity;

                float inscat = inScattering(
                getWorldPosition(),
                shading_view,
                light.worldPosition, d);

                fogColor += light.colorIntensity.rgb * light.colorIntensity.w  * (inscat * inscatteringOpacity) *0.0001;
            }
        }
    }
#endif

#if   defined(BLEND_MODE_OPAQUE)
    // nothing to do here
#elif defined(BLEND_MODE_TRANSPARENT)
    fogColor *= color.a;
#elif defined(BLEND_MODE_ADD)
    fogColor = vec3(0.0);
#elif defined(BLEND_MODE_MASKED)
    // nothing to do here
#elif defined(BLEND_MODE_MULTIPLY)
    // FIXME: unclear what to do here
#elif defined(BLEND_MODE_SCREEN)
    // FIXME: unclear what to do here
#endif

    color.rgb = color.rgb * (1.0 - fogOpacity) + fogColor;

    return color;
}
