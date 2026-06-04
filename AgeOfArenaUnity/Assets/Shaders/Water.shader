Shader "Custom/Water"
{
    Properties
    {
        _Color     ("Shallow Color", Color) = (0.15, 0.40, 0.70, 1)
        _DeepColor ("Deep Color",    Color) = (0.08, 0.20, 0.45, 1)
        _WaveSpeed ("Wave Speed",    Float) = 0.35
        _WaveScale ("Wave Amplitude",Float) = 0.05
        _WaveFreq  ("Wave Frequency",Float) = 0.18
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // N8.terrain: animated ocean surface (Built-in RP surface shader, Lambert).
        // vertex:vert shifts ocean plane verts for a wave ripple effect.
        #pragma surface surf Lambert vertex:vert

        float4 _Color;
        float4 _DeepColor;
        float  _WaveSpeed;
        float  _WaveScale;
        float  _WaveFreq;

        struct Input { float3 worldPos; };

        void vert(inout appdata_full v)
        {
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float wave  = sin(wp.x * _WaveFreq + _Time.y * _WaveSpeed)
                        + cos(wp.z * _WaveFreq * 0.87 + _Time.y * _WaveSpeed * 1.3);
            v.vertex.y += wave * _WaveScale;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            float ripple = sin(IN.worldPos.x * (_WaveFreq * 0.9) +
                               IN.worldPos.z * (_WaveFreq * 0.7) +
                               _Time.y * _WaveSpeed * 0.6) * 0.5 + 0.5;
            o.Albedo    = lerp(_DeepColor.rgb, _Color.rgb, ripple * 0.45 + 0.55);
            o.Gloss     = 0.85;
            o.Specular  = 0.5;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
