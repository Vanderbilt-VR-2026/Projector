// The projector's image on the wall: the wall inside the frame is dimmed (as if the room lights were low
// there) and the image is added on top as light, with the
// imperfections of a real projector: a bright hotspot falling off to the edges, soft focus and bloom,
// color fringing toward the edges, faint scanlines, flicker and drifting dust.
Shader "Projector/Projection Screen"
{
    Properties
    {
        _MainTex ("Projection", 2D) = "black" {}
        _Brightness ("Brightness", Float) = 1.7
        _Hotspot ("Hotspot Falloff", Range(0, 1)) = 0.45
        _Softness ("Soft Focus (mip)", Range(0, 2)) = 0.7
        _Bloom ("Bloom", Range(0, 1)) = 0.45
        _Fringe ("Color Fringe", Range(0, 0.01)) = 0.0025
        _Scanlines ("Scanlines", Range(0, 1)) = 0.1
        _Flicker ("Flicker", Range(0, 0.2)) = 0.035
        _Dust ("Dust", Range(0, 1)) = 0.35
        _Wash ("Black Level", Range(0, 0.2)) = 0.03
        _Ambient ("Wall Kept", Range(0, 1)) = 0.22
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Projection"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Brightness;
                float _Hotspot;
                float _Softness;
                float _Bloom;
                float _Fringe;
                float _Scanlines;
                float _Flicker;
                float _Dust;
                float _Wash;
                float _Ambient;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            // Blur comes from the projection texture's mip chain: smooth at any radius, and cheap on Quest.
            float3 SampleLod(float2 uv, float lod)
            {
                return SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, lod).rgb;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv;
                float2 centered = uv - 0.5;

                // Lens fringing: red and blue drift apart toward the edges; slightly soft focus overall.
                float2 fringe = centered * _Fringe * 2;
                float3 color;
                color.r = SampleLod(uv + fringe, _Softness).r;
                color.g = SampleLod(uv, _Softness).g;
                color.b = SampleLod(uv - fringe, _Softness).b;

                // Light bleeding softly around bright shapes.
                float3 glow = SampleLod(uv, 3) * 0.6 + SampleLod(uv, 4.5) * 0.4;
                color += glow * _Bloom;

                // A projector never quite reaches black.
                color += _Wash;

                // Brightest where the lens points, dimmer toward the corners.
                float2 falloff = centered * float2(1.3, 1.9);
                float hotspot = saturate(1 - _Hotspot * dot(falloff, falloff) * 1.6);

                // Edges of the image are slightly soft rather than razor sharp.
                float2 edge = smoothstep(0, 0.012, uv) * smoothstep(0, 0.012, 1 - uv);

                float scan = 1 - _Scanlines * (0.5 + 0.5 * sin(uv.y * 1400));
                float flicker = 1 - _Flicker * Hash(floor(_Time.yy * 24));

                // Sparse dust specks that jump around a few times a second.
                float speck = Hash(floor(uv * float2(360, 180)) + floor(_Time.y * 3) * 17.0);
                float dust = speck > 0.9985 ? 1 - _Dust : 1;

                float mask = edge.x * edge.y;
                float3 light = color * _Brightness * hotspot * mask * scan * flicker * dust;
                // Alpha dims the wall underneath; it fades out with the image's soft edge.
                return half4(light, (1 - _Ambient) * mask);
            }
            ENDHLSL
        }
    }
}
