#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using MedicalXR.RayMarching;

namespace MedicalXR.EditorScripts
{
    /// <summary>
    /// Generatore procedurale di Texture3D SDF per il paradigma cross-modale Bouba / Kiki.
    /// Produce asset volumetrici (.asset con Texture3D RHalf) compatibili con SdfNode (BakedTexture3D),
    /// SdfVolumeVoxelizer e il raymarching di BioSignalSDF.
    /// </summary>
    public class BoubaKikiTexture3DGenerator : EditorWindow
    {
        public enum ResolutionChoice
        {
            Low_32 = 32,
            Standard_64 = 64,
            High_128 = 128
        }

        [Header("Bake Configuration")]
        public ResolutionChoice resolution = ResolutionChoice.Standard_64;
        public string outputFolder = "Assets/BoubaKiki";

        [Header("Shape Parameters")]
        [Range(0.05f, 0.35f)]
        public float boubaSmoothness = 0.16f;
        [Range(0.05f, 0.20f)]
        public float kikiSpikeBaseRadius = 0.08f;
        [Range(0.25f, 0.48f)]
        public float kikiSpikeLength = 0.42f;

        [MenuItem("MedicalXR/Tools/Bouba-Kiki 3D Texture Generator")]
        public static void OpenWindow()
        {
            var win = GetWindow<BoubaKikiTexture3DGenerator>("Bouba-Kiki SDF Baker");
            win.minSize = new Vector2(420, 480);
            win.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            GUILayout.Label("🧪 BOUBA / KIKI SDF TEXTURE3D BAKER", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Genera Texture3D volumetriche (formato RHalf) che codificano il Signed Distance Field (SDF) " +
                "delle forme archetipiche Bouba (parasimpatico, calmo, organico) e Kiki (simpatico, stress, appuntito).\n\n" +
                "Gli asset .asset generati possono essere assegnati direttamente al campo 'Baked SDF Texture' di un SdfNode " +
                "o nei Template clinici della tesi.", MessageType.Info);

            EditorGUILayout.Space(8);
            resolution = (ResolutionChoice)EditorGUILayout.EnumPopup("Risoluzione Griglia 3D", resolution);
            outputFolder = EditorGUILayout.TextField("Cartella di Output", outputFolder);

            EditorGUILayout.Space(8);
            GUILayout.Label("PARAMETRI MORFOLOGICI", EditorStyles.miniBoldLabel);
            boubaSmoothness = EditorGUILayout.Slider("Bouba Soft Blending (k)", boubaSmoothness, 0.05f, 0.30f);
            kikiSpikeBaseRadius = EditorGUILayout.Slider("Kiki Base Raggio Punte", kikiSpikeBaseRadius, 0.04f, 0.15f);
            kikiSpikeLength = EditorGUILayout.Slider("Kiki Lunghezza Punte", kikiSpikeLength, 0.25f, 0.48f);

            EditorGUILayout.Space(15);
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.95f);
            if (GUILayout.Button("Bake Bouba (Texture3D)", GUILayout.Height(32)))
            {
                BakeBoubaTexture((int)resolution, outputFolder, boubaSmoothness);
            }

            GUI.backgroundColor = new Color(1.0f, 0.35f, 0.35f);
            if (GUILayout.Button("Bake Kiki (Texture3D)", GUILayout.Height(32)))
            {
                BakeKikiTexture((int)resolution, outputFolder, kikiSpikeBaseRadius, kikiSpikeLength);
            }

            GUI.backgroundColor = new Color(1.0f, 0.85f, 0.2f);
            if (GUILayout.Button("Bake Continuum (50% Morph)", GUILayout.Height(28)))
            {
                BakeContinuumTexture((int)resolution, outputFolder, 0.5f, boubaSmoothness, kikiSpikeBaseRadius, kikiSpikeLength);
            }

            EditorGUILayout.Space(10);
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("⭐ BAKE ALL & SPAWN COMPARISON IN SCENE ⭐", GUILayout.Height(38)))
            {
                var boubaTex = BakeBoubaTexture((int)resolution, outputFolder, boubaSmoothness);
                var kikiTex = BakeKikiTexture((int)resolution, outputFolder, kikiSpikeBaseRadius, kikiSpikeLength);
                SpawnComparisonScene(boubaTex, kikiTex);
            }
            GUI.backgroundColor = Color.white;
        }

        // ── Calcolo Matematico Bouba SDF ────────────────────────────────────
        public static float EvaluateBoubaSdf(Vector3 p, float smoothness)
        {
            // 1. Sfera centrale morbida
            float d = p.magnitude - 0.18f;

            // 2. Lobi periferici arrotondati e asimmetrici
            Vector3[] lobes = new Vector3[]
            {
                new Vector3( 0.16f,  0.12f,  0.05f),
                new Vector3(-0.15f,  0.14f, -0.06f),
                new Vector3( 0.12f, -0.16f,  0.08f),
                new Vector3(-0.14f, -0.13f, -0.05f),
                new Vector3( 0.02f,  0.06f,  0.17f),
                new Vector3(-0.05f, -0.08f, -0.17f),
                new Vector3( 0.17f, -0.02f, -0.10f)
            };
            float[] radii = new float[] { 0.14f, 0.13f, 0.15f, 0.13f, 0.14f, 0.13f, 0.12f };

            for (int i = 0; i < lobes.Length; i++)
            {
                float dLobe = (p - lobes[i]).magnitude - radii[i];
                d = Smin(d, dLobe, smoothness);
            }

            return d;
        }

        // ── Calcolo Matematico Kiki SDF ─────────────────────────────────────
        public static float EvaluateKikiSdf(Vector3 p, float baseRadius, float spikeLength)
        {
            // 1. Corpo centrale compatto
            float d = p.magnitude - 0.12f;

            // 2. Cuspidi acuminate lungo direzioni icosaedriche / stellate (14 punte)
            Vector3[] spikeDirs = new Vector3[]
            {
                // Assi cardinali
                new Vector3( 1f,  0f,  0f).normalized,
                new Vector3(-1f,  0f,  0f).normalized,
                new Vector3( 0f,  1f,  0f).normalized,
                new Vector3( 0f, -1f,  0f).normalized,
                new Vector3( 0f,  0f,  1f).normalized,
                new Vector3( 0f,  0f, -1f).normalized,
                // Diagonali spaziali
                new Vector3( 1f,  1f,  1f).normalized,
                new Vector3(-1f,  1f,  1f).normalized,
                new Vector3( 1f, -1f,  1f).normalized,
                new Vector3(-1f, -1f,  1f).normalized,
                new Vector3( 1f,  1f, -1f).normalized,
                new Vector3(-1f,  1f, -1f).normalized,
                new Vector3( 1f, -1f, -1f).normalized,
                new Vector3(-1f, -1f, -1f).normalized
            };

            for (int i = 0; i < spikeDirs.Length; i++)
            {
                Vector3 dir = spikeDirs[i];
                // Alterna leggermente la lunghezza per rendere il solido organico ma minaccioso
                float len = (i % 2 == 0) ? spikeLength : spikeLength * 0.88f;
                float dSpike = SdSharpSpike(p, dir, 0.05f, len, baseRadius);
                d = Smin(d, dSpike, 0.015f); // Blending quasi nullo: punte rigidamente aguzze
            }

            return d;
        }

        private static float SdSharpSpike(Vector3 p, Vector3 dir, float hStart, float hEnd, float rBase)
        {
            float h = Vector3.Dot(p, dir);
            float dPerp = (p - h * dir).magnitude;

            if (h < hStart)
            {
                float dh = hStart - h;
                float dr = Mathf.Max(0f, dPerp - rBase);
                return Mathf.Sqrt(dh * dh + dr * dr);
            }
            if (h > hEnd)
            {
                float dh = h - hEnd;
                return Mathf.Sqrt(dh * dh + dPerp * dPerp);
            }

            float t = (h - hStart) / (hEnd - hStart);
            float rExpected = rBase * Mathf.Pow(1.0f - t, 1.4f); // Profilo esponenzialmente affilato
            float cosAngle = (hEnd - hStart) / Mathf.Sqrt((hEnd - hStart) * (hEnd - hStart) + rBase * rBase);
            return (dPerp - rExpected) * cosAngle;
        }

        private static float Smin(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1.0f - h);
        }

        // ── Baking Pipeline ─────────────────────────────────────────────────
        public static Texture3D BakeBoubaTexture(int res, string folder, float smoothness)
        {
            return BakeSdfGrid(res, folder, "Bouba_3D", (p) => EvaluateBoubaSdf(p, smoothness));
        }

        public static Texture3D BakeKikiTexture(int res, string folder, float baseRadius, float spikeLength)
        {
            return BakeSdfGrid(res, folder, "Kiki_3D", (p) => EvaluateKikiSdf(p, baseRadius, spikeLength));
        }

        public static Texture3D BakeContinuumTexture(int res, string folder, float alpha, float smoothness, float baseRadius, float spikeLength)
        {
            return BakeSdfGrid(res, folder, $"BoubaKiki_Continuum_{(int)(alpha * 100)}", (p) =>
            {
                float dBouba = EvaluateBoubaSdf(p, smoothness);
                float dKiki = EvaluateKikiSdf(p, baseRadius, spikeLength);
                return Mathf.Lerp(dBouba, dKiki, alpha);
            });
        }

        private static Texture3D BakeSdfGrid(int res, string folder, string assetName, System.Func<Vector3, float> sdfFunc)
        {
            EnsureDirectory(folder);
            string fullPath = $"{folder}/{assetName}.asset";

            Texture3D tex = new Texture3D(res, res, res, TextureFormat.RHalf, false);
            tex.name = assetName;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color[] colors = new Color[res * res * res];
            float invRes = 1.0f / (float)res;

            for (int z = 0; z < res; z++)
            {
                float wz = ((float)z + 0.5f) * invRes - 0.5f;
                int zOffset = z * res * res;

                for (int y = 0; y < res; y++)
                {
                    float wy = ((float)y + 0.5f) * invRes - 0.5f;
                    int yOffset = y * res;

                    for (int x = 0; x < res; x++)
                    {
                        float wx = ((float)x + 0.5f) * invRes - 0.5f;
                        Vector3 p = new Vector3(wx, wy, wz);

                        float dist = sdfFunc(p);
                        colors[zOffset + yOffset + x] = new Color(dist, dist, dist, 1f);
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply(false, false);

            AssetDatabase.CreateAsset(tex, fullPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BoubaKiki Baker] Asset Texture3D salvato con successo: {fullPath} ({res}x{res}x{res})");
            return AssetDatabase.LoadAssetAtPath<Texture3D>(fullPath);
        }

        // ── Scene Preview Generator ─────────────────────────────────────────
        public static void SpawnComparisonScene(Texture3D boubaTex, Texture3D kikiTex)
        {
            GameObject root = new GameObject("Bouba_Kiki_Comparison");
            Undo.RegisterCreatedObjectUndo(root, "Spawn Bouba Kiki Comparison");

            // 1. Spawna Bouba sulla sinistra
            GameObject boubaGo = new GameObject("Bouba_Volume");
            boubaGo.transform.SetParent(root.transform);
            boubaGo.transform.localPosition = new Vector3(-0.45f, 0f, 0f);

            var boubaFilter = boubaGo.AddComponent<MeshFilter>();
            var boubaRenderer = boubaGo.AddComponent<MeshRenderer>();
            var boubaController = boubaGo.AddComponent<SdfGroupController>();
            boubaController.renderSolidMesh = true;

            GameObject boubaNodeGo = new GameObject("SdfNode_Bouba");
            boubaNodeGo.transform.SetParent(boubaGo.transform);
            boubaNodeGo.transform.localPosition = Vector3.zero;
            var boubaNode = boubaNodeGo.AddComponent<SdfNode>();
            boubaNode.shapeType = SdfShapeType.BakedTexture3D;
            boubaNode.bakedSdfTexture = boubaTex;
            boubaNode.size = new Vector3(0.25f, 0.25f, 0.25f);
            boubaNode.shapeColor = new Color(0.1f, 0.85f, 0.95f, 1.0f); // Ciano / Calma parasimpatica

            // 2. Spawna Kiki sulla destra
            GameObject kikiGo = new GameObject("Kiki_Volume");
            kikiGo.transform.SetParent(root.transform);
            kikiGo.transform.localPosition = new Vector3(0.45f, 0f, 0f);

            var kikiFilter = kikiGo.AddComponent<MeshFilter>();
            var kikiRenderer = kikiGo.AddComponent<MeshRenderer>();
            var kikiController = kikiGo.AddComponent<SdfGroupController>();
            kikiController.renderSolidMesh = true;

            GameObject kikiNodeGo = new GameObject("SdfNode_Kiki");
            kikiNodeGo.transform.SetParent(kikiGo.transform);
            kikiNodeGo.transform.localPosition = Vector3.zero;
            var kikiNode = kikiNodeGo.AddComponent<SdfNode>();
            kikiNode.shapeType = SdfShapeType.BakedTexture3D;
            kikiNode.bakedSdfTexture = kikiTex;
            kikiNode.size = new Vector3(0.25f, 0.25f, 0.25f);
            kikiNode.shapeColor = new Color(1.0f, 0.22f, 0.22f, 1.0f); // Cremisi / Stress simpatico

            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            Debug.Log("[BoubaKiki Baker] Spawna confronto comparativo Bouba & Kiki nella scena con successo!");
        }

        private static void EnsureDirectory(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                string[] parts = folderPath.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
            }
        }
    }
}
#endif
