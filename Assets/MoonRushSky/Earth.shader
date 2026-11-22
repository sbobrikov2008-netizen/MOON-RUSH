Shader "MOON RUSH/World Earth"
{
    Properties
    {
        [NoScaleOffset] _EarthMap ("Earth day map", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_EarthMap);
            SAMPLER(sampler_EarthMap);

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 localNormal : TEXCOORD0;
            };

            Varyings Vert(float4 vertex : POSITION)
            {
                Varyings result;
                result.position = TransformObjectToHClip(vertex.xyz);
                result.localNormal = vertex.xyz;
                return result;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.localNormal);
                // Fixed texture and sunlight orientation on the sphere.
                const float3 front = float3(-.81092, 0.0, -.58515);
                const float3 right = float3(.58515, 0.0, -.81092);
                float3 globe = float3(dot(normal, right), normal.y, dot(normal, front));
                float longitude = atan2(globe.x, globe.z) + .32;
                float2 uv = float2(.5 + longitude / TWO_PI, .5 + asin(clamp(globe.y, -1.0, 1.0)) / PI);
                half3 albedo = SAMPLE_TEXTURE2D(_EarthMap, sampler_EarthMap, uv).rgb;
                float sun = saturate(dot(globe, normalize(float3(-.52, .38, .76))));
                float limb = pow(saturate(1.0 - globe.z), 4.0) * sun;
                half3 color = albedo * (.032 + .94 * sun) + half3(.015, .04, .075) * limb;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
