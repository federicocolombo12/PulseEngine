Shader "MedicalXR/BioSignalSDF"
{
    Properties
    {
        _MaxSteps ("Max Raymarching Steps", Integer) = 64
        _MinDistance ("Min Draw Distance", Float) = 0.001
        _MaxDistance ("Max Raymarching Distance", Float) = 10.0
        
        _FusionRate ("Fusion Rate (k)", Float) = 0.15

        _HeartRate ("Heart Rate", Float) = 75.0
        _SpO2 ("SpO2 Level", Float) = 98.0
        _PerfusionIndex ("Perfusion Index", Float) = 4.5
        _RWavePulse ("R-Wave Pulse", Float) = 0.0
        _HrvRMSSD ("HRV RMSSD", Float) = 30.0
        _HrvSDNN ("HRV SDNN", Float) = 40.0
        _IsHypoxic ("Is Hypoxic (0 or 1)", Float) = 0.0

        [Header(Surface Texturing Triplanar)]
        _MainTex ("Albedo Texture (Triplanar)", 2D) = "white" {}
        _TextureScale ("Texture Tiling", Float) = 8.0
        _TextureBlend ("Texture Blend", Range(0, 1)) = 0.5
        _TriplanarSharpness ("Triplanar Sharpness", Range(1, 16)) = 4.0

        [Header(Normal Bump Mapping)]
        [Toggle] _EnableBumpMap ("Enable Normal / Bump Map", Float) = 0.0
        _BumpMap ("Normal Map (Triplanar)", 2D) = "bump" {}
        _BumpScale ("Bump Strength", Range(0, 2)) = 0.5

        [Header(MatCap Stylization)]
        [Toggle] _EnableMatCap ("Enable MatCap", Float) = 0.0
        _MatCapTex ("MatCap Texture", 2D) = "black" {}
        _MatCapBlend ("MatCap Blend", Range(0, 1)) = 0.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "RenderPipeline"="UniversalPipeline"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ForwardLit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Properties
            int _MaxSteps;
            float _MinDistance;
            float _MaxDistance;
            float _FusionRate;

            // Bio-signals
            float _HeartRate;
            float _SpO2;
            float _PerfusionIndex;
            float _RWavePulse;
            float _HrvRMSSD;
            float _HrvSDNN;
            float _IsHypoxic;

            // Surface Texturing Properties
            Texture2D _MainTex;
            SamplerState sampler_MainTex;
            float _TextureScale;
            float _TextureBlend;
            float _TriplanarSharpness;

            // Normal Bump Mapping
            Texture2D _BumpMap;
            SamplerState sampler_BumpMap;
            float _EnableBumpMap;
            float _BumpScale;

            // MatCap Stylization
            Texture2D _MatCapTex;
            SamplerState sampler_MatCapTex;
            float _EnableMatCap;
            float _MatCapBlend;

            // Multi-node dynamic arrays (Max 16 nodes for performance in VR)
            #define MAX_NODES 16
            int _NodeCount;
            float4x4 _NodeMatrices[MAX_NODES]; 
            float4x4 _NodeInvMatrices[MAX_NODES];
            float4 _NodeSizes[MAX_NODES];      
            float4 _NodeParams[MAX_NODES]; // x = shapeType or (100+modType), y = combineOp or strength, z = pulse or freq, w = blend or falloff
            float4 _NodeColors[MAX_NODES];
            float _GlobalBlendSoftness;         // Morbidezza k globale fallback
            float3 _VolumeSize;

            Texture3D<float> _NodeTex0;
            Texture3D<float> _NodeTex1;
            Texture3D<float> _NodeTex2;
            Texture3D<float> _NodeTex3;
            Texture3D<float> _NodeTex4;
            Texture3D<float> _NodeTex5;
            Texture3D<float> _NodeTex6;
            Texture3D<float> _NodeTex7;
            Texture3D<float> _NodeTex8;
            Texture3D<float> _NodeTex9;
            Texture3D<float> _NodeTex10;
            Texture3D<float> _NodeTex11;
            Texture3D<float> _NodeTex12;
            Texture3D<float> _NodeTex13;
            Texture3D<float> _NodeTex14;
            Texture3D<float> _NodeTex15;

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 localPos     : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 volumeSize = max(_VolumeSize, 0.001);
                float3 scaledPosOS = input.positionOS.xyz * volumeSize;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(scaledPosOS);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.localPos = scaledPosOS;
                return output;
            }

            // Smooth minimum (Polynomial blend by IQ)
            float smin(float a, float b, float k)
            {
                float h = max(k - abs(a - b), 0.0) / k;
                return min(a, b) - h * h * h * k * (1.0 / 6.0);
            }

            // Smooth maximum (Polynomial blend by IQ)
            float smax(float a, float b, float k)
            {
                float h = max(k - abs(a - b), 0.0) / k;
                return max(a, b) + h * h * h * k * (1.0 / 6.0);
            }

            // ── Primitive distance functions (Inigo Quilez) ──────────────────────────
            float sdSphere(float3 p, float r)
            {
                return length(p) - r;
            }

            float sdBox(float3 p, float3 b)
            {
                float3 q = abs(p) - b;
                return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
            }

            float sdTorus(float3 p, float2 t)
            {
                float2 q = float2(length(p.xz) - t.x, p.y);
                return length(q) - t.y;
            }

            float sdCapsuleY(float3 p, float r, float h)
            {
                float capH = max(0.0, h * 0.5 - r);
                p.y -= clamp(p.y, -capH, capH);
                return length(p) - r;
            }

            float sdCylinderY(float3 p, float r, float h)
            {
                float2 d = abs(float2(length(p.xz), p.y)) - float2(r, h * 0.5);
                return min(max(d.x, d.y), 0.0) + length(max(d, 0.0));
            }

            float evaluateNodeSDF(int index, float3 p)
            {
                int type = (int)_NodeParams[index].x;
                float3 size = _NodeSizes[index].xyz;
                float pulse = _NodeParams[index].z;

                float rawD = 1000.0;
                if (type == 0) // Sphere
                {
                    rawD = sdSphere(p, size.x + pulse * 0.06);
                }
                else if (type == 1) // Box
                {
                    rawD = sdBox(p, size + float3(pulse * 0.05, pulse * 0.05, pulse * 0.05));
                }
                else if (type == 2) // Torus
                {
                    rawD = sdTorus(p, float2(size.x, size.y + pulse * 0.04));
                }
                else if (type == 3) // CapsuleY
                {
                    rawD = sdCapsuleY(p, size.x + pulse * 0.05, size.y);
                }
                else if (type == 4) // CylinderY
                {
                    rawD = sdCylinderY(p, size.x + pulse * 0.05, size.y);
                }
                else if (type == 5) // BakedTexture3D
                {
                    float3 boxSize = max(size * 2.0, 0.0001);
                    float3 uvw = (p / boxSize) + 0.5;
                    float3 clampedUvw = saturate(uvw);
                    
                    float rawDist = 0.0;
                    switch(index)
                    {
                        case 0: rawDist = _NodeTex0.SampleLevel(sampler_LinearClamp, clampedUvw, 0).r; break;
                        case 1: rawDist = _NodeTex1.SampleLevel(sampler_LinearClamp, clampedUvw, 0).r; break;
                        case 2: rawDist = _NodeTex2.SampleLevel(sampler_LinearClamp, clampedUvw, 0).r; break;
                        case 3: rawDist = _NodeTex3.SampleLevel(sampler_LinearClamp, clampedUvw, 0).r; break;
                        case 4: rawDist = _NodeTex4.SampleLevel(sampler_LinearClamp, clampedUvw, 0).r; break;
                        case 5: rawDist = _NodeTex5.SampleLevel(sampler_LinearClamp, clampedUvw, 0).r; break;
                        case 6: rawDist = _NodeTex6.SampleLevel(sampler_LinearClamp, clampedUvw, 0).r; break;
                        case 7: rawDist = _NodeTex7.SampleLevel(sampler_LinearClamp, clampedUvw, 0).r; break;
                        case 8: rawDist = _NodeTex8.SampleLevel(sampler_LinearClamp, clampedUvw, 0).r; break;
                        case 9: rawDist = _NodeTex9.SampleLevel(sampler_LinearClamp, clampedUvw, 0).r; break;
                        case 10: rawDist = _NodeTex10.SampleLevel(sampler_LinearClamp, clampedUvw, 0).r; break;
                        case 11: rawDist = _NodeTex11.SampleLevel(sampler_LinearClamp, clampedUvw, 0).r; break;
                        case 12: rawDist = _NodeTex12.SampleLevel(sampler_LinearClamp, clampedUvw, 0).r; break;
                        case 13: rawDist = _NodeTex13.SampleLevel(sampler_LinearClamp, clampedUvw, 0).r; break;
                        case 14: rawDist = _NodeTex14.SampleLevel(sampler_LinearClamp, clampedUvw, 0).r; break;
                        case 15: rawDist = _NodeTex15.SampleLevel(sampler_LinearClamp, clampedUvw, 0).r; break;
                        default: rawDist = 0.0; break;
                    }
                    
                    float scalingFactor = max(boxSize.x, max(boxSize.y, boxSize.z));
                    float dist = rawDist * scalingFactor;
                    float3 q = max(abs(p) - size, 0.0);
                    dist += length(q);
                    
                    rawD = dist - (pulse * 0.05);
                }

                return rawD;
            }

            // ── MudBun Domain Modifier Volume Deformation ────────────────────────────
            float3 applyModifier(int index, float3 p)
            {
                float4 param = _NodeParams[index]; // x = 100+scope+type, y = strength, z = freq, w = falloff
                int modType = ((int)param.x) % 10;
                float strength = param.y;
                float frequency = param.z;
                float falloff = max(param.w, 0.001);
                float3 size = _NodeSizes[index].xyz;

                float3 pLocal = mul(_NodeMatrices[index], float4(p, 1.0)).xyz;

                float3 dBox = max(abs(pLocal) - size, 0.0);
                float distOutside = length(dBox);
                float influence = 1.0 - smoothstep(0.0, falloff, distOutside);

                if (influence > 0.0001)
                {
                    if (modType == 0) // Twist (around local Y)
                    {
                        float twist = strength * 6.2831853 * influence;
                        float c = cos(twist * pLocal.y);
                        float s = sin(twist * pLocal.y);
                        pLocal.xz = float2(c * pLocal.x - s * pLocal.z, s * pLocal.x + c * pLocal.z);
                    }
                    else if (modType == 1) // Bend (around local Y spine bending in XY)
                    {
                        float bend = strength * 3.14159265 * influence;
                        float cB = cos(bend * pLocal.y);
                        float sB = sin(bend * pLocal.y);
                        pLocal.xy = float2(cB * pLocal.x - sB * pLocal.y, sB * pLocal.x + cB * pLocal.y);
                    }
                    else if (modType == 2) // Noise (3D multi-axis harmonic turbulence)
                    {
                        float freq = max(frequency, 0.1);
                        float3 noiseVal = float3(
                            sin(pLocal.y * freq + 0.35) * cos(pLocal.z * freq * 0.8),
                            sin(pLocal.z * freq + 1.12) * cos(pLocal.x * freq * 0.8),
                            sin(pLocal.x * freq + 2.21) * cos(pLocal.y * freq * 0.8)
                        ) * (strength * influence * 0.1);
                        pLocal += noiseVal;
                    }

                    p = mul(_NodeInvMatrices[index], float4(pLocal, 1.0)).xyz;
                }

                return p;
            }

            // ── Main Scene Map (MudBun Hierarchical Cascade & Domain Volumes) ─────────
            float map(float3 p)
            {
                if (_NodeCount <= 0)
                {
                    return 1000.0;
                }

                float shapeDist[MAX_NODES];
                bool isShape[MAX_NODES];

                // 1. Pass globale: modificatori con scope AllNodesInVolume (type >= 110.0)
                float3 p_global = p;
                for (int m = _NodeCount - 1; m >= 0; m--)
                {
                    if (m >= MAX_NODES) continue;
                    float t_m = _NodeParams[m].x;
                    if (t_m >= 110.0)
                    {
                        p_global = applyModifier(m, p_global);
                    }
                }

                // 2. Pass gerarchico MudBun: per i modificatori PrecedingInHierarchy (100.0 <= type < 110.0)
                float3 p_curr = p_global;
                for (int i = _NodeCount - 1; i >= 0; i--)
                {
                    if (i >= MAX_NODES) continue;
                    float type = _NodeParams[i].x;
                    if (type >= 100.0 && type < 110.0) // PrecedingInHierarchy modifier
                    {
                        p_curr = applyModifier(i, p_curr);
                        isShape[i] = false;
                    }
                    else if (type >= 110.0) // AllNodesInVolume (già applicato globalmente)
                    {
                        isShape[i] = false;
                    }
                    else if (type >= 0.0) // Geometric shape
                    {
                        float3 p_local = mul(_NodeMatrices[i], float4(p_curr, 1.0)).xyz;
                        shapeDist[i] = evaluateNodeSDF(i, p_local);
                        isShape[i] = true;
                    }
                    else
                    {
                        isShape[i] = false;
                    }
                }

                // Forward pass: fusione gerarchica CSG (Smooth/Hard Booleans)
                float d = 1000.0;
                bool firstShape = true;

                for (int j = 0; j < _NodeCount; j++)
                {
                    if (j >= MAX_NODES) break;
                    if (!isShape[j]) continue;

                    float d_j = shapeDist[j];

                    if (firstShape)
                    {
                        d = d_j;
                        firstShape = false;
                        continue;
                    }

                    int op_j = (int)_NodeParams[j].y;
                    float k = _NodeParams[j].w;

                    if (k <= 0.0005 && (op_j == 0 || op_j == 4 || op_j == 5))
                    {
                        k = _GlobalBlendSoftness;
                    }

                    if (k > 0.0005)
                    {
                        if (op_j == 2 || op_j == 4) // Smooth Subtraction
                        {
                            d = smax(d, -d_j, k);
                        }
                        else if (op_j == 3 || op_j == 5) // Smooth Intersection
                        {
                            d = smax(d, d_j, k);
                        }
                        else // Smooth Union
                        {
                            d = smin(d, d_j, k);
                        }
                    }
                    else
                    {
                        if (op_j == 2 || op_j == 4) // Hard Subtraction
                        {
                            d = max(d, -d_j);
                        }
                        else if (op_j == 3 || op_j == 5) // Hard Intersection
                        {
                            d = max(d, d_j);
                        }
                        else // Hard Union
                        {
                            d = min(d, d_j);
                        }
                    }
                }

                return d;
            }

            // Normal estimation
            float3 getNormal(float3 p)
            {
                const float2 k = float2(1.0, -1.0);
                const float eps = 0.0015;
                return normalize(
                    k.xyy * map(p + k.xyy * eps) +
                    k.yyx * map(p + k.yyx * eps) +
                    k.yxy * map(p + k.yxy * eps) +
                    k.xxx * map(p + k.xxx * eps)
                );
            }

            // Calcola il colore del nodo geometricamente combinato (con smooth blending)
            float3 getNodeColor(float3 p)
            {
                if (_NodeCount <= 0)
                {
                    return float3(0.0, 0.8, 0.95);
                }

                float shapeDist[MAX_NODES];
                float3 shapeCol[MAX_NODES];
                bool isShape[MAX_NODES];

                // 1. Pass globale: modificatori con scope AllNodesInVolume (type >= 110.0)
                float3 p_global = p;
                for (int m = _NodeCount - 1; m >= 0; m--)
                {
                    if (m >= MAX_NODES) continue;
                    float t_m = _NodeParams[m].x;
                    if (t_m >= 110.0)
                    {
                        p_global = applyModifier(m, p_global);
                    }
                }

                // 2. Pass gerarchico MudBun: per i modificatori PrecedingInHierarchy (100.0 <= type < 110.0)
                float3 p_curr = p_global;
                for (int i = _NodeCount - 1; i >= 0; i--)
                {
                    if (i >= MAX_NODES) continue;
                    float type = _NodeParams[i].x;
                    if (type >= 100.0 && type < 110.0)
                    {
                        p_curr = applyModifier(i, p_curr);
                        isShape[i] = false;
                    }
                    else if (type >= 110.0)
                    {
                        isShape[i] = false;
                    }
                    else if (type >= 0.0)
                    {
                        float3 p_local = mul(_NodeMatrices[i], float4(p_curr, 1.0)).xyz;
                        shapeDist[i] = evaluateNodeSDF(i, p_local);
                        shapeCol[i] = _NodeColors[i].rgb;
                        isShape[i] = true;
                    }
                    else
                    {
                        isShape[i] = false;
                    }
                }

                float d = 1000.0;
                float3 col = float3(0, 0, 0);
                bool firstShape = true;

                for (int j = 0; j < _NodeCount; j++)
                {
                    if (j >= MAX_NODES) break;
                    if (!isShape[j]) continue;

                    float d_j = shapeDist[j];
                    float3 col_j = shapeCol[j];

                    if (firstShape)
                    {
                        d = d_j;
                        col = col_j;
                        firstShape = false;
                        continue;
                    }

                    int op_j = (int)_NodeParams[j].y;
                    float k = _NodeParams[j].w;

                    if (k <= 0.0005 && (op_j == 0 || op_j == 4 || op_j == 5))
                    {
                        k = _GlobalBlendSoftness;
                    }

                    if (k > 0.0005)
                    {
                        if (op_j == 2 || op_j == 4) // Smooth Subtraction
                        {
                            float mixFactor = clamp(0.5 + 0.5 * (d + d_j) / k, 0.0, 1.0);
                            d = smax(d, -d_j, k);
                            col = lerp(col_j, col, mixFactor);
                        }
                        else if (op_j == 3 || op_j == 5) // Smooth Intersection
                        {
                            float mixFactor = clamp(0.5 + 0.5 * (d_j - d) / k, 0.0, 1.0);
                            d = smax(d, d_j, k);
                            col = lerp(col, col_j, mixFactor);
                        }
                        else // Smooth Union
                        {
                            float mixFactor = clamp(0.5 + 0.5 * (d_j - d) / k, 0.0, 1.0);
                            d = smin(d, d_j, k);
                            col = lerp(col_j, col, mixFactor);
                        }
                    }
                    else
                    {
                        if (op_j == 2 || op_j == 4) // Hard Subtraction
                        {
                            if (-d_j > d) col = col_j;
                            d = max(d, -d_j);
                        }
                        else if (op_j == 3 || op_j == 5) // Hard Intersection
                        {
                            if (d_j > d) col = col_j;
                            d = max(d, d_j);
                        }
                        else // Hard Union
                        {
                            if (d_j < d) col = col_j;
                            d = min(d, d_j);
                        }
                    }
                }

                return col;
            }

            // ── Raymarching Loop ─────────────────────────────────────────────────────
            float4 frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                float3 volumeSize = max(_VolumeSize, 0.001);
                float3 cameraPosWS = GetCameraPositionWS();
                float3 cameraPosLocal = TransformWorldToObject(cameraPosWS).xyz;
                
                // Determina se la telecamera è entrata all'interno del volume
                bool cameraInside = all(abs(cameraPosLocal) <= volumeSize * 0.5);

                // Quando la camera è esterna, scarta i back-face per evitare doppio calcolo.
                // Quando la camera è interna, i front-face sono tagliati dal near plane, quindi renderizza sui back-face.
                if (!isFrontFace && !cameraInside)
                {
                    discard;
                }

                float3 rayStartLocal = cameraInside ? cameraPosLocal : input.localPos;
                
                // Calcolo raggio robusto compatibile con telecamere Perspective e Orthographic (SceneView)
                float3 rayDirLocal;
                if (unity_OrthoParams.w > 0.5)
                {
                    rayDirLocal = TransformWorldToObjectDir(-GetViewForwardDir());
                }
                else
                {
                    rayDirLocal = normalize(input.localPos - cameraPosLocal);
                }

                float t = 0.0;
                float3 p = rayStartLocal;
                bool hit = false;
                
                // Massima distanza di attraversamento del volume
                float maxRayDist = max(_MaxDistance, length(volumeSize) * 2.5);

                for (int i = 0; i < _MaxSteps; i++)
                {
                    p = rayStartLocal + rayDirLocal * t;
                    float d = map(p);
                    
                    // Soglia di hit adattiva proporzionale alla distanza per convergenza perfetta sia vicino che lontano
                    float minThreshold = max(_MinDistance, t * 0.0005);
                    if (d < minThreshold)
                    {
                        hit = true;
                        break;
                    }
                    
                    t += d;
                    
                    // Margine di contenimento del volume aumentato a 0.75 per evitare falsi scarti sui bordi
                    if (t > maxRayDist || any(abs(p) > volumeSize * 0.75))
                    {
                        break;
                    }
                }

                if (!hit)
                {
                    discard;
                }

                // Shading anatomico e geometrico ad alta definizione
                float3 normal = getNormal(p);

                // --- TEXTURIZZAZIONE TRIPLANARE E BUMP MAPPING ---
                // Calcolo pesi triplanari (Nvidia GPU Gems 3 / Unity standard)
                // Sottrae una soglia per limitare la zona di transizione ed evitare sovrapposizioni sbiadite
                float3 blendWeights = max(0.0, abs(normal) - 0.2);
                blendWeights = pow(blendWeights, max(_TriplanarSharpness, 1.0));
                blendWeights /= max(dot(blendWeights, 1.0), 0.00001);

                // Coordinate di campionamento triplanare (Convenzione standard Mikkelsen):
                // X-plane (proiezione assiale X): (z, y)
                // Y-plane (proiezione assiale Y): (x, z)
                // Z-plane (proiezione assiale Z): (x, y)
                float2 uvX = float2(p.z, p.y) * _TextureScale;
                float2 uvY = float2(p.x, p.z) * _TextureScale;
                float2 uvZ = float2(p.x, p.y) * _TextureScale;

                // Applicazione Normal / Bump Mapping via Surface Gradient (Mikkelsen 2013 / JCGT 2020)
                // Nessuna discontinuità o seam sui cambi di quadrante (niente sign() o salti)
                if (_EnableBumpMap > 0.5)
                {
                    float4 bumpSampleX = _BumpMap.SampleLevel(sampler_BumpMap, uvX, 0);
                    float4 bumpSampleY = _BumpMap.SampleLevel(sampler_BumpMap, uvY, 0);
                    float4 bumpSampleZ = _BumpMap.SampleLevel(sampler_BumpMap, uvZ, 0);

                    float3 nX = UnpackNormalScale(bumpSampleX, _BumpScale);
                    float3 nY = UnpackNormalScale(bumpSampleY, _BumpScale);
                    float3 nZ = UnpackNormalScale(bumpSampleZ, _BumpScale);

                    // Derivate spaziali dh/du, dh/dv
                    float2 derivX = -nX.xy / max(nX.z, 0.001);
                    float2 derivY = -nY.xy / max(nY.z, 0.001);
                    float2 derivZ = -nZ.xy / max(nZ.z, 0.001);

                    // Gradiente volumetrico combinato
                    float3 volumeGrad = float3(
                        blendWeights.z * derivZ.x + blendWeights.y * derivY.x,
                        blendWeights.z * derivZ.y + blendWeights.x * derivX.y,
                        blendWeights.x * derivX.x + blendWeights.y * derivY.y
                    );

                    // Proiezione ortogonale sul piano tangente (Surface Gradient)
                    float3 surfGrad = (volumeGrad - dot(normal, volumeGrad) * normal) * saturate(_BumpScale);

                    // Risoluzione della normale finale perturbata
                    normal = normalize(normal - surfGrad);
                }

                // Reattività all'illuminazione di scena URP (Directional Light)
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(TransformWorldToObjectDir(mainLight.direction));
                float3 lightColor = mainLight.color;

                float NdotL = dot(normal, lightDir);
                float diffuse = max(0.0, NdotL);
                float halfLambert = pow(NdotL * 0.5 + 0.5, 2.0);

                float3 viewDirLocal = -rayDirLocal;
                float3 halfDir = normalize(lightDir + viewDirLocal);
                float spec = pow(max(0.0, dot(normal, halfDir)), 32.0);
                float NdotV = saturate(dot(normal, viewDirLocal));
                float fresnel = pow(1.0 - NdotV, 3.5);

                // Colore del nodo più vicino / fusione CSG
                float3 nodeColor = getNodeColor(p);

                // Albedo Triplanare (modulazione con tinta del nodo)
                if (_TextureBlend > 0.001)
                {
                    float4 colX = _MainTex.SampleLevel(sampler_MainTex, uvX, 0);
                    float4 colY = _MainTex.SampleLevel(sampler_MainTex, uvY, 0);
                    float4 colZ = _MainTex.SampleLevel(sampler_MainTex, uvZ, 0);
                    float4 triplanarAlbedo = colX * blendWeights.x + colY * blendWeights.y + colZ * blendWeights.z;

                    float3 texturedColor = nodeColor * triplanarAlbedo.rgb;
                    nodeColor = lerp(nodeColor, texturedColor, _TextureBlend);
                }

                // MatCap Stylization (opzionale)
                if (_EnableMatCap > 0.5 && _MatCapBlend > 0.001)
                {
                    float3 normalWS = TransformObjectToWorldNormal(normal);
                    float3 normalVS = TransformWorldToViewDir(normalWS);
                    float2 matCapUv = normalVS.xy * 0.5 + 0.5;
                    float3 matCapColor = _MatCapTex.SampleLevel(sampler_MatCapTex, matCapUv, 0).rgb;

                    float3 mcTinted = matCapColor * nodeColor * 1.5;
                    nodeColor = lerp(nodeColor, mcTinted, _MatCapBlend);
                }

                // --- SISTEMA CROMATICO BIO-SIGNAL & NODE COLORS ---
                // Luce ambiente calibrata per evitare neri assoluti nelle ombre in XR
                float3 ambientLight = float3(0.15, 0.16, 0.20);
                float3 directLight = lightColor * (diffuse * 0.85 + halfLambert * 0.25);
                float3 baseColor = nodeColor * (directLight + ambientLight);

                float3 glowColor = nodeColor * fresnel * (_PerfusionIndex * 0.25 + 0.2);
                float3 specColor = lightColor * spec * 0.35;
                float3 healthyColor = baseColor + glowColor + specColor;

                // Battito cardiaco: flash reattivo basato sul colore del nodo
                healthyColor += nodeColor * (_RWavePulse * 0.5);

                // Overlay ipossia (stato di stress/allarme)
                float warningFlash = sin(_Time.y * 12.0) * 0.5 + 0.5;
                float3 hypoxicColor = lerp(float3(0.8, 0.0, 0.1), float3(1.0, 0.35, 0.0), warningFlash) * (diffuse + 0.15);
                hypoxicColor += float3(1.0, 0.0, 0.0) * fresnel * 0.7;

                float3 finalColor = lerp(healthyColor, hypoxicColor, _IsHypoxic);

                // Mesh solida: alpha pieno per evitare buchi trasparenti sulle creste del bump
                float alpha = 1.0;

                return float4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
}
