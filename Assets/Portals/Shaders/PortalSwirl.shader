Shader "Portals/PortalSwirl"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.1, 0.55, 1, 1)
        _InnerColor ("Inner Color", Color) = (0.05, 0.2, 0.6, 1)
        _OuterColor ("Outer Color", Color) = (0.4, 0.85, 1, 1)
        _RimColor ("Rim Color", Color) = (0.7, 0.95, 1, 1)
        _PortalViewTex ("Portal View Texture", 2D) = "black" {}
        _HasPortalView ("Has Portal View", Float) = 0
        _SwirlSpeed ("Swirl Speed", Float) = 1.2
        _SwirlIntensity ("Swirl Intensity", Float) = 1.0
        _NoiseScale ("Noise Scale", Float) = 4.0
        _RimWidth ("Rim Width", Range(0.01, 0.3)) = 0.08
        _CenterAlpha ("Center Alpha", Range(0, 1)) = 0.18
        _MiddleAlpha ("Middle Alpha", Range(0, 1)) = 0.55
        _OuterAlpha ("Outer Alpha", Range(0, 1)) = 0.95
        _Emission ("Emission", Float) = 1.0
        _ClipPlane ("Clip Plane (xyz normal, w distance)", Vector) = (0,0,1,10000)
        _ClipSide ("Clip Side", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "PortalSwirl"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _InnerColor;
                float4 _OuterColor;
                float4 _RimColor;
                float4 _ClipPlane;
                float _HasPortalView;
                float _SwirlSpeed;
                float _SwirlIntensity;
                float _NoiseScale;
                float _RimWidth;
                float _CenterAlpha;
                float _MiddleAlpha;
                float _OuterAlpha;
                float _Emission;
                float _ClipSide;
            CBUFFER_END

            TEXTURE2D(_PortalViewTex);
            SAMPLER(sampler_PortalViewTex);

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.uv = IN.uv;
                return OUT;
            }

            float hash(float2 p){ return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f*f*(3.0-2.0*f);
                float a = hash(i);
                float b = hash(i+float2(1,0));
                float c = hash(i+float2(0,1));
                float d = hash(i+float2(1,1));
                return lerp(lerp(a,b,u.x), lerp(c,d,u.x), u.y);
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // Clip against arbitrary plane for split transitions.
                if (_ClipSide > 0.5)
                {
                    float d = dot(_ClipPlane.xyz, IN.positionWS) + _ClipPlane.w;
                    if (d < 0) discard;
                }
                else if (_ClipSide < -0.5)
                {
                    float d = dot(_ClipPlane.xyz, IN.positionWS) + _ClipPlane.w;
                    if (d > 0) discard;
                }

                float2 p = IN.uv * 2.0 - 1.0;
                float r = length(p);
                float a = atan2(p.y, p.x);
                float t = _Time.y;

                // Layered noise.
                float n1 = noise(p * _NoiseScale + float2(t*0.3, t*0.2));
                float n2 = noise(p * (_NoiseScale*2.1) - float2(t*0.5, -t*0.4));
                float n = (n1 + n2*0.5) / 1.5;

                // Swirl distortion in polar coordinates.
                float swirl = a + _SwirlIntensity * sin(r*6.0 - t*_SwirlSpeed) + (n-0.5)*1.2;
                float band = 0.5 + 0.5 * sin(swirl*3.0 + r*8.0 - t*1.5);

                // Energy veins.
                float veins = pow(saturate(1.0 - abs(sin(a*7.0 + r*12.0 - t*2.2))), 3.0);

                float3 col = lerp(_InnerColor.rgb, _OuterColor.rgb, smoothstep(0.0, 0.85, r));
                col = lerp(col, _BaseColor.rgb, band * 0.35);
                col += _RimColor.rgb * veins * 0.6;
                col *= (0.8 + 0.4 * n);

                // Rim.
                float rim = smoothstep(1.0 - _RimWidth, 1.0, r);
                col = lerp(col, _RimColor.rgb, rim);
                float rimEmit = rim * _Emission;

                // Portal view texture in center.
                if (_HasPortalView > 0.5)
                {
                    float2 viewUV = p * 0.5 + 0.5;
                    float3 viewCol = SAMPLE_TEXTURE2D(_PortalViewTex, sampler_PortalViewTex, viewUV).rgb;
                    float viewMask = smoothstep(0.95, 0.55, r);
                    col = lerp(col, viewCol, viewMask * 0.85);
                }

                // Alpha layering: center translucent, middle visible, outer opaque.
                float alpha = lerp(_CenterAlpha, _MiddleAlpha, smoothstep(0.0, 0.45, r));
                alpha = lerp(alpha, _OuterAlpha, smoothstep(0.45, 0.85, r));
                alpha = saturate(alpha + veins * 0.25 + rim * 0.2);
                // Soft edge.
                alpha *= smoothstep(1.05, 0.92, r);

                float3 finalCol = col * (1.0 + _Emission * (0.5 + 0.5*band) + rimEmit);
                return float4(finalCol, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
