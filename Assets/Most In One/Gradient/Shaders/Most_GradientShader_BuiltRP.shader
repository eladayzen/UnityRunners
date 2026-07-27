
Shader "MOST/MOST_Gradient_BuiltRP"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color1 ("Color A", Color) = (1,1,1,1)
        _Color2 ("Color B", Color) = (1,1,1,1)
        [Toggle] _IsGlobal ("Global Y Start Point", Float) = 1.0
        [Toggle] _ScaleIndependent ("Scale Independent", Float) = 1.0
        _Offset("Offset", Float) = 0.0
        _Scale ("Gradient Area Length", Float) = 1.0
        _Angle ("Gradient Angle", Range(0, 360)) = 0.0
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjectors"="True"
            "RenderType"="Transparent"
        }
        LOD 100
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass // Depth Pass
        {
            Name "DEPTHPREPASS"
            ZWrite On
            ColorMask 0
            Cull Back
    
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
    
            struct v2f 
            {
                float4 pos : SV_POSITION;
            };
    
            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }
    
            fixed4 frag(v2f i) : SV_Target
            {
                return 0;
            }
            ENDCG
        }
        
        Pass // Main pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD2;
                float3 localPos : TEXCOORD3;
                SHADOW_COORDS(4)
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color1;
            float4 _Color2;
            float _IsGlobal;
            float _ScaleIndependent;
            float _GlobalRotationPoint;
            float _Offset;
            float _Scale;
            float _Angle;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.localPos = v.vertex.xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                TRANSFER_SHADOW(o)
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float angleRad = radians(_Angle);
                float2x2 rotMatrix = float2x2(
                    cos(angleRad), -sin(angleRad),
                    sin(angleRad), cos(angleRad)
                );
                
                float scale = _ScaleIndependent? _Scale * length(unity_ObjectToWorld._m01_m11_m21) : _Scale;

                float Ycen, gradientFactor;
                float2 rotatedPos, pos;

                if(_IsGlobal)
                {
                    pos.y = i.worldPos.y - _Offset;
                    pos.x = i.worldPos.x;
                    rotatedPos = mul(rotMatrix, pos) ;
                    gradientFactor = ((rotatedPos.y - scale * 0.5) / -scale);
                }
                else
                {
                    pos = i.localPos.xy;
                    Ycen = _Offset;
                    rotatedPos = mul(rotMatrix, pos * length(unity_ObjectToWorld._m01_m11_m21) - Ycen) ;
                    gradientFactor = (rotatedPos.y - scale * 0.5) / -scale;
                }

                gradientFactor = saturate(gradientFactor);
                fixed4 gradient = lerp(_Color1, _Color2, gradientFactor);
                fixed4 col = tex2D(_MainTex, i.uv) * gradient;

                fixed shadow = SHADOW_ATTENUATION(i);
                col.rgb *= shadow;

                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }

        Pass // Shadow casting pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    Fallback "VertexLit"
}
