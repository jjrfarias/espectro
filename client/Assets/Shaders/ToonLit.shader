Shader "Espectro/ToonLit"
{
    // Shader toon/cel-shaded pra URP: iluminação em degraus (em vez de gradiente PBR) + contorno
    // (segunda passada com as normais invertidas/expandidas). _BaseMap/_BaseColor têm as mesmas
    // tags ([MainTexture]/[MainColor]) do URP/Lit, então todo código existente que já faz
    // material.mainTexture = x ou material.color = x continua funcionando sem mudança nenhuma.
    // _AlphaClip/_Cutoff também seguem a mesma convenção usada pela folhagem (ver
    // StylizedVisualBootstrap.ConfigureAlphaClip).
    Properties
    {
        [MainTexture] _BaseMap("Textura Base", 2D) = "white" {}
        [MainColor] _BaseColor("Cor Base", Color) = (1,1,1,1)

        [Toggle(_ALPHATEST_ON)] _AlphaClip("Recorte Alpha", Float) = 0
        _Cutoff("Limiar do Recorte", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Faces", Float) = 2

        _ShadowColor("Cor da Sombra", Color) = (0.55, 0.52, 0.65, 1)
        _Bands("Degraus de Luz", Range(2, 6)) = 3
        _RimColor("Cor do Contorno de Luz", Color) = (1, 0.96, 0.85, 1)
        _RimPower("Intensidade do Contorno de Luz", Range(0.5, 8)) = 3
        _RimStrength("Forca do Contorno de Luz", Range(0, 1)) = 0.16

        _OutlineColor("Cor do Contorno", Color) = (0.05, 0.05, 0.08, 1)
        _OutlineWidth("Espessura do Contorno", Range(0, 0.03)) = 0.006
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        // --- Passada 1: contorno (casco invertido, sem preencher o objeto por dentro) ---
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _OutlineColor;
                float _OutlineWidth;
                float _Cutoff;
                float _AlphaClip;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings OutlineVert(Attributes IN)
            {
                Varyings OUT;
                // A extrusão precisa acontecer DEPOIS de ir pro espaço do mundo, não antes: objetos
                // deste jogo têm escala local muito variada (o pacote Nature, por exemplo, chega a
                // escalar 200-700x pra compensar um mesh importado minúsculo). Se a extrusão fosse
                // feita em espaço do objeto (antes da escala), a mesma matriz de escala do modelo
                // multiplicava a espessura do contorno junto — virava um blob preto do tamanho do
                // objeto inteiro em vez de uma borda fina. Extrudindo em espaço do mundo, a
                // espessura fica sempre a mesma em unidades reais, não importa a escala do objeto.
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                positionWS += normalWS * _OutlineWidth;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 OutlineFrag(Varyings IN) : SV_Target
            {
                // Sem isso, a folhagem (cartão recortado por alpha) desenhava um contorno sólido
                // no formato do quad inteiro por trás do recorte — virava uma silhueta preta em
                // vez de só a borda da folha. O contorno precisa recortar igual ao preenchimento.
                // O valor também é testado em runtime: materiais importados são criados
                // depois do build e a variante por keyword pode ter sido removida.
                if (_AlphaClip > 0.5) discard;
                return _OutlineColor;
            }
            ENDHLSL
        }

        // --- Passada 2: preenchimento toon (degraus de luz + contorno de luz/rim) ---
        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ToonVert
            #pragma fragment ToonFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Cutoff;
                float _AlphaClip;
                float4 _ShadowColor;
                float _Bands;
                float4 _RimColor;
                float _RimPower;
                float _RimStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings ToonVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 ToonFrag(Varyings IN) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half4 albedo = texColor * _BaseColor;
                if (_AlphaClip > 0.5) clip(albedo.a - _Cutoff);

                float3 normalWS = normalize(IN.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float ndotl = saturate(dot(normalWS, mainLight.direction));
                float lit = saturate(ndotl * mainLight.shadowAttenuation);
                float banded = floor(lit * _Bands) / max(_Bands - 1, 1);
                banded = saturate(banded);

                half3 shaded = lerp(_ShadowColor.rgb, mainLight.color, banded) * albedo.rgb;

                float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower) * banded;
                shaded += rim * _RimColor.rgb * _RimStrength;

                return half4(shaded, albedo.a);
            }
            ENDHLSL
        }

        // Sombra própria (pra outros objetos toon receberem sombra deste), reaproveitando o pass
        // padrão de shadow caster da pipeline em vez de escrever um do zero.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }

    FallBack "Universal Render Pipeline/Lit"
}
