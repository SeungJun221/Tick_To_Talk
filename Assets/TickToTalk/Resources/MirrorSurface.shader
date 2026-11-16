Shader "TickToTalk/MirrorSurface"
{
    Properties { _ReflectionTex("Reflection", 2D) = "gray" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4x4 _ReflectionVP;
            TEXTURE2D(_ReflectionTex); SAMPLER(sampler_ReflectionTex);
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 screen : TEXCOORD0; };
            Varyings vert(Attributes input) {
                Varyings o; o.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                o.screen=mul(_ReflectionVP,float4(TransformObjectToWorld(input.positionOS.xyz),1)); return o;
            }
            half4 frag(Varyings input) : SV_Target {
                float2 uv=input.screen.xy/input.screen.w * .5 + .5;
                #if UNITY_UV_STARTS_AT_TOP
                uv.y=1-uv.y;
                #endif
                half3 color=SAMPLE_TEXTURE2D(_ReflectionTex,sampler_ReflectionTex,uv).rgb;
                return half4(max(color,half3(.035,.045,.055))*half3(.94,.98,1),1);
            }
            ENDHLSL
        }
    }
}