// :)
Shader "MOST/MOST_Skybox"
{
	Properties
	{
		_Color1 ("Sky Color", Color) = (1,1,1,1)
		_Color2 ("Ground Color", Color) = (1,1,1,1)
		_SkyPos ("Sky Position", Range(-1.0, 1.0)) = 0.5
		_GroundPos ("Ground Position", Range(-1.0, 1.0)) = -0.5
	}
	SubShader
	{
		Tags
		{
			"Queue"="Background"
		}
		Pass
		{
			ZWrite Off
			Cull Back
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			uniform float4 _Color1;
			uniform float4 _Color2;
			uniform float _SkyPos;
			uniform float _GroundPos;

			struct In
			{
				float4 localPos: POSITION;
				float3 texcoord: TEXCOORD0;
			};

			struct Out
			{
				float4 clipPos: SV_POSITION;
				float3 texcoord: TEXCOORD0;
			};

			Out vert(In v)
			{
				Out o;
				o.clipPos = UnityObjectToClipPos(v.localPos);
				o.texcoord = v.texcoord;
				return o;
			}

			float4 frag(Out v): COLOR
			{
				return lerp(_Color2, _Color1, clamp((v.texcoord.y-_GroundPos)/(_SkyPos-_GroundPos),0,1));
			}

			ENDCG
		}
	}
}