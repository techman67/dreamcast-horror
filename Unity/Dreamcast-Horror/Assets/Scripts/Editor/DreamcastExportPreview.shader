Shader "Hidden/DreamcastExportPreview" {
 Properties { _MainTex ("Texture", 2D) = "white" {} }
 SubShader { Tags { "RenderType"="Opaque" } Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex;
 struct input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
 struct output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
 output vert(input v) { output o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
 fixed4 frag(output i):SV_Target {
 float4 c=tex2D(_MainTex,i.uv)*i.color;
 #ifndef UNITY_COLORSPACE_GAMMA
 c.rgb=GammaToLinearSpace(c.rgb);
 #endif
 return float4(c.rgb,1);
 }
 ENDCG
 } }
}
