Shader "Custom/FogOfWar"
{
    Properties
    {
        _Color   ("Color", Color) = (0.357, 0.549, 0.243, 1)
        _FogTex  ("Fog Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        CGPROGRAM
        // noambient: ambient/SH are disabled so unexplored (fog=0) areas are
        // truly black — directional sun is the only light source.
        #pragma surface surf Lambert noambient

        fixed4     _Color;
        sampler2D  _FogTex;

        struct Input
        {
            float3 worldPos;
        };

        void surf(Input IN, inout SurfaceOutput o)
        {
            // World spans -60..+60 in X and Z (120×120 units).
            float2 uv = (IN.worldPos.xz + 60.0) / 120.0;
            float  fog = tex2D(_FogTex, uv).r;

            // Multiply base colour by fog: 0=black, ~0.27=shroud, 1=full.
            o.Albedo = _Color.rgb * fog;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
