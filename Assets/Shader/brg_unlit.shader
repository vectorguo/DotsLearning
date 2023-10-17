Shader "brg/brg_unlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            //--------------------------------------
            // GPU Instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Shader Stages
            #pragma vertex vert
            #pragma fragment frag

            // #if defined(UNITY_DOTS_INSTANCING_ENABLED)
            // UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
            // UNITY_DOTS_INSTANCED_PROP(float4, Color)
            // UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
            // #endif

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _Color;
            CBUFFER_END

            v2f vert (appdata v)
            {
                v2f o = (v2f)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                half4 col = tex2D(_MainTex, i.uv);
                col.rgb *= _Color.rgb;
                return col;
            }
            ENDHLSL
        }
    }
}
