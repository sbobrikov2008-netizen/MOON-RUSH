Shader "MOON RUSH/Construction Preview"
{
    Properties
    {
        _Tint ("Placement tint", Color) = (.2,.85,.6,.45)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END

            struct Varyings
            {
                float4 position : SV_POSITION;
                half light : TEXCOORD0;
            };

            Varyings Vert(float4 position : POSITION, float3 normal : NORMAL)
            {
                Varyings output;
                output.position = TransformObjectToHClip(position.xyz);
                output.light = .62 + .38 * saturate(dot(TransformObjectToWorldNormal(normal), normalize(float3(.2,.9,.4))));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(_Tint.rgb * input.light, _Tint.a);
            }
            ENDHLSL
        }
    }
}
