// URP 17.6: official Lit shading with a conservative texture-color skin mask.
// _SkinTone is a Vector on purpose: values are direct linear RGB multipliers.
Shader "CJEndlessRunner/Reference Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Base color", Color) = (1,1,1,1)
        _SkinTone("Skin multiplier (linear RGB)", Vector) = (.42,.42,.35,1)
        _SkinStrength("Skin correction strength", Range(0,1)) = 1
        _SkinMask("Skin mask (RG min/max, GB min/max)", Vector) = (.02,.12,.015,.12)
        [Toggle(_NORMALMAP)] _UseNormalMap("Use normal map", Float) = 1
        [Normal] _BumpMap("Normal map", 2D) = "bump" {}
        _BumpScale("Normal strength", Float) = 1
        [Toggle(_METALLICSPECGLOSSMAP)] _UseMetallicMap("Use metallic/smoothness map", Float) = 1
        _MetallicGlossMap("Metallic (R), smoothness (A)", 2D) = "white" {}
        _Metallic("Metallic without map", Range(0,1)) = 0
        _Smoothness("Smoothness multiplier", Range(0,1)) = .35
        [Toggle(_OCCLUSIONMAP)] _UseOcclusionMap("Use occlusion map", Float) = 0
        _OcclusionMap("Occlusion (G)", 2D) = "white" {}
        _OcclusionStrength("Occlusion strength", Range(0,1)) = 1
        [Toggle(_EMISSION)] _UseEmission("Use emission", Float) = 0
        [HDR] _EmissionColor("Emission color", Color) = (0,0,0,1)
        _EmissionMap("Emission map", 2D) = "white" {}
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha clipping", Float) = 0
        _Cutoff("Alpha cutoff", Range(0,1)) = .5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [Toggle(_RECEIVE_SHADOWS_OFF)] _DisableReceiveShadows("Disable received shadows", Float) = 0
        [HideInInspector] _ReceiveShadows("Receive shadows", Float) = 1
        [HideInInspector] _WorkflowMode("Workflow", Float) = 1
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _SpecColor("Specular", Color) = (.2,.2,.2,1)
        [HideInInspector] _SpecGlossMap("Specular map", 2D) = "white" {}
        [HideInInspector] _Parallax("Parallax", Float) = .005
        [HideInInspector] _DetailAlbedoMap("Detail albedo", 2D) = "linearGrey" {}
        [HideInInspector] _DetailAlbedoMapScale("Detail albedo scale", Float) = 1
        [HideInInspector] _DetailNormalMapScale("Detail normal scale", Float) = 1
        [HideInInspector] _ClearCoatMask("Clear coat", Float) = 0
        [HideInInspector] _ClearCoatSmoothness("Clear coat smoothness", Float) = 0
        [HideInInspector][NoScaleOffset] unity_Lightmaps("Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("Lightmap direction", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks("Shadow masks", 2DArray) = "" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit" }
        LOD 300
        Cull [_Cull]

        HLSLINCLUDE
        // Preload every URP 17.6 LitInput dependency before temporarily extending
        // CBUFFER_END. LitInput itself declares exactly one UnityPerMaterial buffer.
        // This keeps custom parameters in that same buffer and all passes compatible
        // with the SRP Batcher, without copying or changing Unity's package files.
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        // Core does not declare the sRGB conversions before our surface wrapper.
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ParallaxMapping.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/SurfaceType.hlsl"
        #undef CBUFFER_END
        #define CBUFFER_END float4 _SkinTone; float4 _SkinMask; float _SkinStrength; };
        #define InitializeStandardLitSurfaceData InitializeOriginalCJLitSurfaceData
        #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
        #undef InitializeStandardLitSurfaceData
        #undef CBUFFER_END
        #define CBUFFER_END };

        inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
        {
            InitializeOriginalCJLitSurfaceData(uv, surface);
            // Classify the untinted source texture in perceptual RGB. Fixed absolute
            // thresholds in linear RGB miss darker skin because channel gaps shrink.
            half3 color = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).rgb;
            #if !defined(UNITY_COLORSPACE_GAMMA)
                color = LinearToSRGB(color);
            #endif
            half redDominance = smoothstep(_SkinMask.x, max(_SkinMask.x + .001, _SkinMask.y), color.r - color.g);
            half blueDeficit = smoothstep(_SkinMask.z, max(_SkinMask.z + .001, _SkinMask.w), color.g - color.b);
            half largest = max(color.r, max(color.g, color.b));
            half smallest = min(color.r, min(color.g, color.b));
            half saturation = (largest - smallest) / max(largest, .001h);
            half mask = redDominance * blueDeficit * smoothstep(.06h, .20h, saturation)
                * smoothstep(.025h, .08h, largest);
            half3 multiplier = lerp(half3(1,1,1), max(_SkinTone.rgb, half3(0,0,0)), saturate(mask * _SkinStrength));
            #if defined(UNITY_COLORSPACE_GAMMA)
                surface.albedo = LinearToSRGB(SRGBToLinear(surface.albedo) * multiplier);
            #else
                surface.albedo *= multiplier;
            #endif
        }
        ENDHLSL

        // ForwardOnly also renders correctly in a deferred URP renderer.
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma multi_compile _ _WRITE_SMOOTHNESS
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode"="MotionVectors" }
            ColorMask RG
            HLSLPROGRAM
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
