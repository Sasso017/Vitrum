// Proiezione triplanare: applica la texture proiettandola nello spazio del mondo da tre direzioni
// (alto, fronte, lato) e fondendole dove si incontrano. Non usa le UV del modello:
// adatto a legno, pietra e metallo su modelli senza uno sviluppo UV curato.
// La ripetizione è espressa in "volte per metro", quindi resta uguale qualunque sia la scala del modello.
Shader "Basilica/Triplanare"
{
    Properties
    {
        _Color ("Colore", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Scala ("Ripetizioni per metro", Float) = 1
        _Nitidezza ("Nitidezza delle giunture", Range(1, 16)) = 6
        _Metallic ("Metallic", Range(0,1)) = 0
        _Glossiness ("Smoothness", Range(0,1)) = 0.2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Scala, _Nitidezza, _Metallic, _Glossiness;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 p = IN.worldPos * _Scala;

            // Peso di ciascuna proiezione in base all'orientamento della superficie
            float3 w = pow(abs(normalize(IN.worldNormal)), _Nitidezza);
            w /= (w.x + w.y + w.z);

            fixed4 lato   = tex2D(_MainTex, p.zy);
            fixed4 alto   = tex2D(_MainTex, p.xz);
            fixed4 fronte = tex2D(_MainTex, p.xy);
            fixed4 c = (lato * w.x + alto * w.y + fronte * w.z) * _Color;

            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
