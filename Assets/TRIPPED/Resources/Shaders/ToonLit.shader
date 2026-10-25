// Cel-shaded lit surface for the 3D house kit: the sun lights each face in two bands with a soft edge, the shaded
// band leans towards a pastel shadow tint instead of going grey, room lights add soft banded pools, and a light rim
// picks out edges - the bright anime look of the art references, with readable shapes.
// Blend, ZWrite and Cull are material properties named like URP Lit's, so the see-through fader (OccluderFader)
// fades it the same way it fades Lit.
// Target: URP 17 (Universal renderer, Forward+), PC. Budget: 1 texture sample per fragment, none in the light loop.
// Keywords (all URP's own, the same set SimpleLit compiles): main/additional light shadows and soft-shadow quality,
// additional lights per pixel or vertex, the Forward+ cluster loop, fog, instancing; GBuffer normal packing in
// DepthNormals (for SSAO). No keywords of its own.
// Written for TRIPPED! (post-jam look pass).
Shader "TRIPPED/ToonLit"
{
    Properties
    {
        [Header(Surface)]
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Colour", Color) = (1, 1, 1, 1)

        [Header(Cel Shading)]
        _ShadowColor ("Shadow Tint", Color) = (0.72, 0.66, 0.85, 1)
        _ShadeThreshold ("Shade Threshold (N.L)", Range(-1, 1)) = 0.15
        _ShadeSoftness ("Shade Edge Softness", Range(0.001, 1)) = 0.06
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 1
        _PointSoftness ("Room Light Band Softness", Range(0.001, 1)) = 0.35
        _PointReach ("Room Light Reach Boost", Range(1, 8)) = 4

        [Header(Rim)]
        _RimColor ("Rim Colour", Color) = (1, 0.95, 0.91, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.3
        _RimPower ("Rim Tightness", Range(0.5, 8)) = 3

        // Render state, driven like URP Lit (see OccluderFader).
        [HideInInspector] _Surface ("__surface", Float) = 0
        [HideInInspector] _Blend ("__blend", Float) = 0
        [HideInInspector] _SrcBlend ("__src", Float) = 1
        [HideInInspector] _DstBlend ("__dst", Float) = 0
        [HideInInspector] _SrcBlendAlpha ("__srcA", Float) = 1
        [HideInInspector] _DstBlendAlpha ("__dstA", Float) = 0
        [HideInInspector] _ZWrite ("__zw", Float) = 1
        [HideInInspector] _Cull ("__cull", Float) = 2
        [HideInInspector] _AlphaClip ("__clip", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadowColor;
            half _ShadeThreshold;
            half _ShadeSoftness;
            half4 _RimColor;
            half _RimStrength;
            half _RimPower;
            half _AmbientStrength;
            half _PointSoftness;
            half _PointReach;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = GetVertexNormalInputs(v.normalOS).normalWS;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // A room light's soft banded pool: what the light gives is eased into a band instead of a long falloff. The
            // band is then faded by the light's own distance attenuation, boosted by _PointReach so the pool stays full
            // across most of the room and only fades near the edge of the light's range.
            half3 PointBand(Light light, float3 normalWS)
            {
                half ndl = saturate(dot(normalWS, light.direction));
                half amount = ndl * light.distanceAttenuation * light.shadowAttenuation;
                return light.color * smoothstep(0.0h, _PointSoftness, amount) * saturate(light.distanceAttenuation * _PointReach);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                float3 n = normalize(i.normalWS);
                float3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);

                // Sun: two bands with a soft edge; the shaded band leans to the pastel tint.
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half ndl = dot(n, sun.direction);
                half lit = smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, ndl) * sun.shadowAttenuation;
                half3 light = SampleSH(n) * _AmbientStrength + sun.color * lit;
                half3 color = albedo.rgb * light * lerp(_ShadowColor.rgb, 1.0h, lit);

                // Room lights.
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                half4 shadowMask = half4(1, 1, 1, 1);
                half3 rooms = 0;
                #if USE_CLUSTER_LIGHT_LOOP
                for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
                    rooms += PointBand(GetAdditionalLight(dirIndex, i.positionWS, shadowMask), n);
                #endif
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    rooms += PointBand(GetAdditionalLight(lightIndex, i.positionWS, shadowMask), n);
                LIGHT_LOOP_END
                color += albedo.rgb * rooms;

                // Rim on the lit side picks out edges.
                half rim = pow(1.0h - saturate(dot(n, viewDir)), _RimPower) * _RimStrength * lit;
                color += _RimColor.rgb * rim;

                color = MixFog(color, i.fogFactor);
                return half4(color, albedo.a);
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            float4 vert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                float3 lightDir = _LightDirection;
                #endif
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir)));
            }

            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            float4 vert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                return TransformObjectToHClip(v.positionOS.xyz);
            }

            half frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            // For SSAO (the PC renderer has it on).
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = NormalizeNormalPerPixel(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = saturate(PackNormalOctQuadEncode(n) * 0.5 + 0.5);
                return half4(PackFloat2To888(oct), 0.0);
                #else
                return half4(n, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
