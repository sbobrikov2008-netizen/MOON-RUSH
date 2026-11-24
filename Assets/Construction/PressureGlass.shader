Shader "MOON RUSH/Pressure Glass"
{
    Properties
    {
        _BaseColor ("Glazing tint and opacity", Color) = (.24,.56,.69,.12)
        _ReflectionTint ("Grazing-angle tint", Color) = (.79,.87,.93,1)
        _EdgeOpacity ("Grazing-angle opacity", Range(0,1)) = .29
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+15" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ReflectionTint;
                half _EdgeOpacity;
            CBUFFER_END

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
                half3 normal : TEXCOORD1;
            };

            Varyings Vert(float4 position : POSITION, float3 normal : NORMAL)
            {
                Varyings result;
                result.worldPosition = TransformObjectToWorld(position.xyz);
                result.position = TransformWorldToHClip(result.worldPosition);
                result.normal = TransformObjectToWorldNormal(normal);
                return result;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 view = SafeNormalize(GetWorldSpaceViewDir(input.worldPosition));
                half angle = 1.0h - saturate(dot(normalize(input.normal), view));
                half fresnel = angle * angle * angle;
                half3 reflection = lerp(_BaseColor.rgb, _ReflectionTint.rgb, fresnel);
                return half4(reflection, saturate(_BaseColor.a + fresnel * _EdgeOpacity));
            }
            ENDHLSL
        }
    }
}
