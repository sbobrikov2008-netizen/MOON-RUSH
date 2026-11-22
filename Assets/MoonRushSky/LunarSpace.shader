Shader "MOON RUSH/Lunar Space"
{
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(float4 vertex : POSITION)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(vertex);
                // Keep only the orientation of the skybox mesh. Translation (including
                // Unity recentering the skybox at the eye) cannot affect celestial directions.
                output.direction = mul((float3x3)unity_ObjectToWorld, vertex.xyz);
                return output;
            }

            // A direction-fixed sparse star field. Screen derivatives soften subpixel stars.
            float Hash(float2 p)
            {
                float3 q = frac(p.xyx * .1031);
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 ray = normalize(input.direction);
                float2 projected = ray.xz / (1.0 + abs(ray.y)) * 315.0;
                float2 cell = floor(projected);
                float seed = Hash(cell + floor(ray.y + 1.0) * 17.0);
                float2 offset = .23 + .54 * float2(Hash(cell + 19.4), Hash(cell + 44.7));
                float footprint = max(length(fwidth(projected)), .012);
                float size = lerp(.018, .075, seed * seed);
                float profile = 1.0 - smoothstep(size, size + min(footprint, .18), length(frac(projected) - offset));
                float stars = step(.91, seed) * profile * lerp(.013, .13, pow(seed, 18.0))
                    * saturate(.44 / footprint);
                half3 color = stars * half3(.92, .96, 1.0);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
