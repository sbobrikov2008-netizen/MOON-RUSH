Shader "MOON RUSH/Lunar Space"
{
    Properties
    {
        [NoScaleOffset] _EarthMap ("Earth day map", 2D) = "white" {}
    }
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

            sampler2D _EarthMap;

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

                // Azimuth 35 degrees, elevation 1.82 degrees, angular diameter about two degrees.
                float3 axis = normalize(float3(.57329, .03176, .81874));
                float along = dot(ray, axis);
                float radius = .0175;
                float3 lateral = ray - axis * along;
                float squared = dot(lateral, lateral) / (radius * radius);
                float edge = max(fwidth(squared), .001);
                if (along > 0.0 && squared < 1.0 + edge)
                {
                    float3 right = normalize(cross(float3(0.0, 1.0, 0.0), axis));
                    float3 up = cross(axis, right);
                    float3 normal = normalize(float3(dot(lateral, right) / radius, dot(lateral, up) / radius,
                        sqrt(saturate(1.0 - squared))));
                    float longitude = atan2(normal.x, normal.z) + .32;
                    float2 uv = float2(frac(.5 + longitude / 6.2831853), .5 + asin(normal.y) / 3.14159265);
                    half3 earth = tex2D(_EarthMap, uv).rgb;
                    float sun = saturate(dot(normal, normalize(float3(-.52, .38, .76))));
                    float phase = .032 + .94 * sun;
                    float rim = pow(saturate(1.0 - normal.z), 4.0) * sun;
                    half3 planet = earth * phase + half3(.015, .04, .075) * rim;
                    color = lerp(color, planet, 1.0 - smoothstep(1.0 - edge, 1.0 + edge, squared));
                }
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
