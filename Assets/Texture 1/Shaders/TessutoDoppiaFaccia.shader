// Shader per superfici sottili senza spessore (tessuti, mantelli, foglie):
// le disegna da entrambi i lati, con ritaglio in base all'alfa (Cutout).
// Il retro viene illuminato correttamente invertendo la normale.
Shader "Basilica/Tessuto Doppia Faccia"
{
    Properties
    {
        _Color ("Colore", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB) Trasparenza (A)", 2D) = "white" {}
        _Cutoff ("Soglia di ritaglio", Range(0,1)) = 0.5
        [NoScaleOffset] _MetallicGlossMap ("Metallic (R) Smoothness (A)", 2D) = "black" {}
        _Metallic ("Metallic (senza mappa)", Range(0,1)) = 0
        _Glossiness ("Smoothness (senza mappa)", Range(0,1)) = 0.2
        _GlossMapScale ("Moltiplicatore Smoothness", Range(0,1)) = 1
        [NoScaleOffset] [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Intensità Normal", Float) = 1
        [Toggle] _UsaMetallicMap ("Usa la mappa Metallic", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "IgnoreProjector"="True" }
        LOD 300
        Cull Off

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alphatest:_Cutoff addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _MetallicGlossMap;
        sampler2D _BumpMap;
        fixed4 _Color;
        half _Metallic, _Glossiness, _GlossMapScale, _BumpScale, _UsaMetallicMap;

        struct Input
        {
            float2 uv_MainTex;
            float facing : VFACE;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;

            if (_UsaMetallicMap > 0.5)
            {
                fixed4 mg = tex2D(_MetallicGlossMap, IN.uv_MainTex);
                o.Metallic = mg.r;
                o.Smoothness = mg.a * _GlossMapScale;
            }
            else
            {
                o.Metallic = _Metallic;
                o.Smoothness = _Glossiness;
            }

            float3 n = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
            // Sul retro la normale va invertita, altrimenti il tessuto risulta scuro
            n.z *= IN.facing > 0 ? 1 : -1;
            o.Normal = n;
        }
        ENDCG
    }

    FallBack "Legacy Shaders/Transparent/Cutout/VertexLit"
}
