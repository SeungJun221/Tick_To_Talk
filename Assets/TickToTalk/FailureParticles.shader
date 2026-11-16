Shader "TickToTalk/Failure Particles URP"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        _TintColor ("Tint", Color) = (.5,.5,.5,.5)
        _SrcBlend ("Source Blend", Float) = 5
        _DstBlend ("Destination Blend", Float) = 10
        _AlphaOnly ("Alpha Texture", Float) = 0
        _Multiply ("Smoke Multiply", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _TintColor;
                float _AlphaOnly, _Multiply;
            CBUFFER_END
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.color=v.color; o.uv=TRANSFORM_TEX(v.uv,_MainTex); return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half4 tex=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
                if(_Multiply>.5)
                {
                    if(_AlphaOnly>.5)return i.color*tex.a;
                    half mask=tex.a*i.color.a;
                    tex.rgb*=i.color.rgb*_TintColor.rgb;
                    return lerp(half4(.5,.5,.5,.5),tex,mask);
                }
                if(_AlphaOnly>.5)tex=tex.aaaa;
                half4 col=2*i.color*_TintColor*tex;
                col.a=saturate(col.a); return col;
            }
            ENDHLSL
        }
    }
}
