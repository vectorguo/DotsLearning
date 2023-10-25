Shader "brg/brg_unlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Lightmaps ("Lightmap", 2DArray) = "" {}
        _LightmapST("LightmapST", Vector) = (0,0,0,0)
        _LightmapIndex("LightmapIndex", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "Forward"

            Cull Off

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            //--------------------------------------
            // Lightmap
            #pragma multi_compile __ LIGHTMAP_ON 

            // -------------------------------------
            // Shader Stages
            #pragma vertex vert
            #pragma fragment frag

            struct a2v
            {
                float4 vertex   : POSITION;
                float2 uv       : TEXCOORD0;
                float2 uv2      : TEXCOORD01;
                
            #if defined(UNITY_DOTS_INSTANCING_ENABLED)
				uint instanceID : INSTANCEID_SEMANTIC;
			#endif
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float2 uv       : TEXCOORD0;

            #if defined(UNITY_DOTS_INSTANCING_ENABLED)
                half2  lmap	    : TEXCOORD1;
				uint instanceID : CUSTOM_INSTANCE_ID;
            #elif LIGHTMAP_ON
                half2  lmap	    : TEXCOORD1;
            #endif
};

            sampler2D _MainTex;

            TEXTURE2D_ARRAY(_Lightmaps);
            SAMPLER(sampler_Lightmaps);

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _LightmapST;
            float _LightmapIndex;
            CBUFFER_END

        #if defined(UNITY_DOTS_INSTANCING_ENABLED)
            UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                UNITY_DOTS_INSTANCED_PROP(float4, _LightmapST)
                UNITY_DOTS_INSTANCED_PROP(float , _LightmapIndex)
            UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

            #define _LightmapST     UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _LightmapST)
            #define _LightmapIndex  UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _LightmapIndex)
        #endif

            v2f vert (a2v v)
            {
                v2f o = (v2f)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

            #if defined(UNITY_DOTS_INSTANCING_ENABLED)
                o.lmap = v.uv2.xy * _LightmapST.xy + _LightmapST.zw;
            #elif LIGHTMAP_ON
                o.lmap = v.uv2.xy * unity_LightmapST.xy + unity_LightmapST.zw;
            #endif
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                half4 col = tex2D(_MainTex, i.uv);

            #if defined(UNITY_DOTS_INSTANCING_ENABLED)
                half4 decodeInstructions = half4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0h, 0.0h);
                half3 lm = DecodeLightmap(SAMPLE_TEXTURE2D_ARRAY(_Lightmaps, sampler_Lightmaps, i.lmap, (int)_LightmapIndex), decodeInstructions);
                col.rgb *= lm;
            #elif LIGHTMAP_ON
                half4 decodeInstructions = half4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0h, 0.0h);
                half3 lm = DecodeLightmap(SAMPLE_TEXTURE2D(unity_Lightmap, samplerunity_Lightmap, i.lmap), decodeInstructions);
                col.rgb *= lm;
            #endif

                return col;
            }
            ENDHLSL
        }
    }
}
