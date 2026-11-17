// The faint cone of light between the projector lens and the wall, as a soft glowing volume rather than a solid
// with hard sides. Each pixel adds up the light along its view ray through the beam: brightest near the lens,
// fading toward the wall, and fading out smoothly around the edge of the projected image instead of stopping
// at a hard boundary. The mesh only bounds the beam (a cone a little wider than the light); the light itself
// is computed from _Lens, _Axis (lens to image center), _Across and _Up (half the image's width and height).
Shader "Projector/Projector Beam"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 0.97, 0.9, 1)
        _Intensity ("Intensity", Float) = 0.03
        _Softness ("Edge Softness", Range(0.01, 0.5)) = 0.18
        _WallFade ("Brightness at the Wall", Range(0, 1)) = 0.14
        _Lens ("Lens (world)", Vector) = (0, 0, 0, 0)
        _Axis ("Lens to Image Center (world)", Vector) = (0, 0, 1, 0)
        _Across ("Half Image Width (world)", Vector) = (1, 0, 0, 0)
        _Up ("Half Image Height (world)", Vector) = (0, 1, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Beam"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define STEPS 14
            // Longest stretch of beam a view ray can cross (metres): end to end, looking down its length.
            #define MAX_THICKNESS 10.0

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Intensity;
                float _Softness;
                float _WallFade;
                float4 _Lens;
                float4 _Axis;
                float4 _Across;
                float4 _Up;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            // Where a point sits in the beam: s runs 0 at the lens to 1 at the image plane, and (x, y) is where the
            // lens projects the point onto the image, -1..1 across its width and height.
            // The image plane's normal, scaled so dot(d, it) gives s directly.
            float3 ImagePlane()
            {
                float3 normal = cross(_Across.xyz, _Up.xyz);
                return normal / dot(_Axis.xyz, normal);
            }

            float3 BeamCoordinates(float3 p, float3 plane)
            {
                float3 d = p - _Lens.xyz;
                float s = dot(d, plane);
                float3 onImage = d / max(s, 1e-4) - _Axis.xyz;
                return float3(dot(onImage, _Across.xyz) / dot(_Across.xyz, _Across.xyz), dot(onImage, _Up.xyz) / dot(_Up.xyz, _Up.xyz), s);
            }

            // How much light is in the beam at a point: zero outside it, fading smoothly across the image's edge.
            float Density(float3 p, float3 plane)
            {
                float3 b = BeamCoordinates(p, plane);
                if (b.z <= 0 || b.z >= 1)
                    return 0;
                // A rounded rectangle: 1 on the image's edge.
                float2 a = abs(b.xy);
                float2 a2 = a * a;
                float2 q = a2 * a2 * a;
                float r = pow(q.x + q.y, 0.2);
                float edge = 1 - smoothstep(1 - _Softness, 1 + _Softness, r);
                // Light thins out as it spreads toward the wall, and fades in just past the lens.
                float along = lerp(1, _WallFade, b.z) * smoothstep(0, 0.04, b.z);
                return edge * along;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 camera = GetCameraPositionWS();
                float3 toSurface = input.positionWS - camera;
                float toSurfaceLength = length(toSurface);
                float3 direction = toSurface / toSurfaceLength;
                // Which side of the cone this is, from its outward normal (SV_IsFrontFace can flip when Unity
                // renders into a texture).
                bool frontFace = dot(input.normalWS, direction) < 0;

                // From outside, the near side of the cone starts the ray into the beam; from inside, the camera
                // does and the far side ends it. (The far side seen from outside is skipped, or it'd count twice.)
                float3 plane = ImagePlane();
                float3 b = BeamCoordinates(camera, plane);
                bool inside = b.z > 0 && b.z < 1 && max(abs(b.x), abs(b.y)) < 1 + _Softness * 2;
                float3 start;
                float span;
                if (frontFace && !inside)
                {
                    start = input.positionWS;
                    span = MAX_THICKNESS;
                }
                else if (!frontFace && inside)
                {
                    start = camera;
                    span = toSurfaceLength;
                }
                else
                {
                    discard;
                }

                // A different offset per pixel turns the steps' banding into fine, even grain.
                float stepLength = span / STEPS;
                float3 p = start + direction * stepLength * Hash(input.positionCS.xy);
                float light = 0;
                [unroll]
                for (int i = 0; i < STEPS; i++)
                {
                    light += Density(p, plane);
                    p += direction * stepLength;
                }
                return half4(_Tint.rgb * light * stepLength * _Intensity, 1);
            }
            ENDHLSL
        }
    }
}
