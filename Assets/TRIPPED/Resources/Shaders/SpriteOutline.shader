// Unlit sprite with a thin outline drawn into its own transparent texels: the pixel-art characters and appliances
// get a white "sticker" edge so they sit on the smooth 3D rooms. Width is in texels, set per renderer by SpriteStyle.
// Target: URP (Universal renderer, forward), PC. Budget: 9 texture samples per fragment, no keywords or variants.
Shader "TRIPPED/SpriteOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _OutlineColor ("Outline Colour", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Outline Width (texels)", Float) = 1
        _Silhouette ("Silhouette Only, 0 or 1 (drawn behind a multi-part rig)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline"
               "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            half4 _OutlineColor;
            float _OutlineWidth;
            half _Silhouette;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half Alpha(float2 uv) { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a; }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                c.rgb *= i.color.rgb;
                float2 d = _MainTex_TexelSize.xy * _OutlineWidth;
                float2 e = d * 0.7071;
                half n = max(max(Alpha(i.uv + float2(d.x, 0)), Alpha(i.uv - float2(d.x, 0))),
                             max(Alpha(i.uv + float2(0, d.y)), Alpha(i.uv - float2(0, d.y))));
                n = max(n, max(max(Alpha(i.uv + e), Alpha(i.uv - e)),
                               max(Alpha(i.uv + float2(e.x, -e.y)), Alpha(i.uv + float2(-e.x, e.y)))));
                half edge = n * _OutlineColor.a * (1 - c.a);
                half a = c.a + edge;
                half3 rgb = (c.rgb * c.a + _OutlineColor.rgb * edge) / max(a, 1e-4);
                // Silhouette mode: the sprite and its edge as one flat outline-coloured shape. A rig draws one of
                // these behind all its parts, so no edge shows where an arm overlaps the body.
                half4 sticker = half4(rgb, a);
                half4 silhouette = half4(_OutlineColor.rgb, max(c.a, n) * _OutlineColor.a);
                half4 o = lerp(sticker, silhouette, _Silhouette);
                return half4(o.rgb, o.a * i.color.a);
            }
            ENDHLSL
        }
    }
}
