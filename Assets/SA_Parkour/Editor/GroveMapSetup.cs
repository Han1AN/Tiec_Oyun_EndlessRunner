// Map preparation only: no player, motorcycle controller or game logic.
// Generates a separate preview scene and prefab without modifying the user's open scene.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TIEC.MapArt
{
    [Serializable] internal class GroveMaterialData
    {
        public string name;
        public float[] rgba;
        public string texture;
        public float emission;
        public string normal_texture;
        public string metallic_smoothness_texture;
        public float roughness;
        public float metallic;
    }

    [Serializable] internal class GroveRampData
    {
        public int id;
        public float x, y, length, height, speed;
        public float[] landing;
    }

    [Serializable] internal class GroveWorkData
    {
        public string id;
        public float y_min, y_max;
        public int[] lanes;
    }

    [Serializable] internal class GroveMarkerData { public string name; public float[] position; }

    [Serializable] internal class GroveMapData
    {
        public float metres;
        public GroveMaterialData[] material_data;
        public GroveRampData[] ramps;
        public GroveWorkData[] workzones;
        public GroveMarkerData[] markers;
    }

    internal sealed class GroveModelImporter : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] previous)
        {
            foreach (var path in imported)
                if (path == "Assets/SA_Parkour/map_data.json" || path.StartsWith("Assets/SA_Parkour/Models/", StringComparison.Ordinal))
                { GroveMapSetup.SchedulePreparation(); break; }
        }
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/SA_Parkour/Textures/Vehicles/", StringComparison.Ordinal) &&
                !assetPath.StartsWith("Assets/SA_Parkour/Textures/City/", StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            bool normal = assetPath.IndexOf("Normal_OpenGL", StringComparison.OrdinalIgnoreCase) >= 0;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal && assetPath.IndexOf("Metallic", StringComparison.OrdinalIgnoreCase) < 0 &&
                assetPath.IndexOf("Roughness", StringComparison.OrdinalIgnoreCase) < 0;
            // Green was already converted in source data; never invert it a second time.
            importer.flipGreenChannel = false;
        }
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/SA_Parkour/Models/", StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }
    }

    [InitializeOnLoad]
    public static class GroveMapSetup
    {
        private const string BasePath = "Assets/SA_Parkour";
        private const string ScenePath = "Assets/Scenes/Grove_Run_Map.unity";
        private const string PrefabPath = BasePath + "/Prefabs/Grove_Run_Map.prefab";
        private static double nextCheck;

        static GroveMapSetup()
        {
            SchedulePreparation();
        }

        internal static void SchedulePreparation()
        {
            // Re-arm after a later map export, including when the editor remains open.
            nextCheck = EditorApplication.timeSinceStartup + 2;
            EditorApplication.update -= PrepareOnce;
            EditorApplication.update += PrepareOnce;
        }

        private static void PrepareOnce()
        {
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 2;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string versionPath = ScenePath + ".version.txt";
            string jsonPath = BasePath + "/map_data.json";
            if (!File.Exists(jsonPath)) return;
            string version = Hash128.Compute(File.ReadAllText(jsonPath)).ToString();
            if (File.Exists(ScenePath) && File.Exists(versionPath) && File.ReadAllText(versionPath) == version)
            { EditorApplication.update -= PrepareOnce; return; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BasePath + "/Models/Grove_Run_Environment.fbx") == null) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BasePath + "/Models/Grove_Run_Collisions.fbx") == null) return;
            if (AssetDatabase.LoadAssetAtPath<TextAsset>(BasePath + "/map_data.json") == null) return;
            EditorApplication.update -= PrepareOnce;
            try { BuildScene(false); }
            catch (Exception error) { Debug.LogException(error); }
        }

        [MenuItem("Tools/TIEC Map/Open Grove Run Map")]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath)) BuildScene(false);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Tools/TIEC Map/Rebuild Grove Run Assets")]
        public static void Rebuild() { BuildScene(false); }

        public static void BuildScene(bool unused)
        {
            var visualAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BasePath + "/Models/Grove_Run_Environment.fbx");
            var collisionAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BasePath + "/Models/Grove_Run_Collisions.fbx");
            var dataAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(BasePath + "/map_data.json");
            if (visualAsset == null || collisionAsset == null || dataAsset == null)
                throw new InvalidOperationException("Grove Run model assets have not finished importing.");
            var data = JsonUtility.FromJson<GroveMapData>(dataAsset.text);
            MakeFolder(BasePath + "/Materials");
            MakeFolder(BasePath + "/Prefabs");
            MakeFolder("Assets/Scenes");
            var materials = new System.Collections.Generic.Dictionary<string, Material>();
            foreach (var definition in data.material_data)
            {
                var path = BasePath + "/Materials/" + definition.name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
                else mat.shader = shader;
                mat.name = definition.name;
                var rgba = definition.rgba;
                var color = new Color(rgba[0], rgba[1], rgba[2], rgba[3]);
                var texture = string.IsNullOrEmpty(definition.texture) ? null :
                    AssetDatabase.LoadAssetAtPath<Texture2D>(BasePath + "/Textures/" + definition.texture);
                if (definition.name == "Asphalt" || definition.name == "Concrete") color = Color.white;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
                float smoothness = 1f - Mathf.Clamp01(definition.roughness);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", definition.metallic);
                var normal = string.IsNullOrEmpty(definition.normal_texture) ? null :
                    AssetDatabase.LoadAssetAtPath<Texture2D>(BasePath + "/Textures/" + definition.normal_texture);
                var metallic = string.IsNullOrEmpty(definition.metallic_smoothness_texture) ? null :
                    AssetDatabase.LoadAssetAtPath<Texture2D>(BasePath + "/Textures/" + definition.metallic_smoothness_texture);
                if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", normal);
                if (normal != null) mat.EnableKeyword("_NORMALMAP"); else mat.DisableKeyword("_NORMALMAP");
                if (mat.HasProperty("_MetallicGlossMap")) mat.SetTexture("_MetallicGlossMap", metallic);
                if (metallic != null)
                {
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP"); mat.EnableKeyword("_METALLICGLOSSMAP");
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 1f);
                    if (mat.HasProperty("_GlossMapScale")) mat.SetFloat("_GlossMapScale", 1f);
                    if (mat.HasProperty("_SmoothnessTextureChannel")) mat.SetFloat("_SmoothnessTextureChannel", 0f);
                }
                else { mat.DisableKeyword("_METALLICSPECGLOSSMAP"); mat.DisableKeyword("_METALLICGLOSSMAP"); }
                if (definition.name == "Palm_Leaf" || definition.name == "Leaf_Light")
                {
                    if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)CullMode.Off);
                    mat.doubleSidedGI = true;
                }
                if (definition.emission > 0)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", color * definition.emission);
                }
                EditorUtility.SetDirty(mat);
                materials.Add(definition.name, mat);
            }

            var previousScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var root = new GameObject("Grove Run Map — 1 unit = 1 metre");
                var visuals = (GameObject)PrefabUtility.InstantiatePrefab(visualAsset, scene);
                visuals.name = "Environment";
                visuals.transform.SetParent(root.transform, false);
                foreach (var renderer in visuals.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++)
                        if (slots[i] != null && materials.TryGetValue(slots[i].name, out var replacement)) slots[i] = replacement;
                    renderer.sharedMaterials = slots;
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,
                        StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                }

                var collisions = (GameObject)PrefabUtility.InstantiatePrefab(collisionAsset, scene);
                collisions.name = "Collision Proxies (invisible)";
                collisions.transform.SetParent(root.transform, false);
                foreach (var renderer in collisions.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                foreach (var filter in collisions.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null) continue;
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    collider.convex = false;
                    filter.gameObject.isStatic = true;
                }

                var markers = new GameObject("Map Markers"); markers.transform.SetParent(root.transform, false);
                if (data.markers != null) foreach (var marker in data.markers)
                    AddMarker(markers.transform, marker.name,
                        new Vector3(marker.position[0], marker.position[1], marker.position[2]));
                // Empty markers allow the game developer to attach their own controller/trigger logic.
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

                var sunObject = new GameObject("Warm Afternoon Sun");
                var sun = sunObject.AddComponent<Light>(); sun.type = LightType.Directional;
                sun.color = new Color(1, .79f, .55f); sun.intensity = 1.5f;
                sun.shadows = LightShadows.Soft; sunObject.transform.rotation = Quaternion.Euler(36, -30, 0);
                RenderSettings.sun = sun;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(.55f, .65f, .73f);
                RenderSettings.ambientEquatorColor = new Color(.51f, .39f, .28f);
                RenderSettings.ambientGroundColor = new Color(.22f, .24f, .18f);
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = new Color(.75f, .62f, .44f);
                RenderSettings.fogStartDistance = 140; RenderSettings.fogEndDistance = 350;
                var cameraObject = new GameObject("Map Preview Camera");
                var camera = cameraObject.AddComponent<Camera>(); cameraObject.tag = "MainCamera";
                cameraObject.transform.position = new Vector3(1, 5, -10);
                cameraObject.transform.LookAt(new Vector3(0, 2.5f, 46));
                camera.fieldOfView = 58; camera.farClipPlane = 1000;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.78f, .58f, .35f);
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveScene(scene, ScenePath);
                File.WriteAllText(ScenePath + ".version.txt", Hash128.Compute(dataAsset.text).ToString());
                File.WriteAllText(BasePath + "/unity_transfer_result.json",
                    "{\"status\":\"passed\",\"scene\":\"" + ScenePath + "\",\"colliders\":" +
                    collisions.GetComponentsInChildren<MeshCollider>().Length + ",\"renderers\":" +
                    visuals.GetComponentsInChildren<Renderer>().Length + "}");
                Debug.Log("GROVE_MAP_READY: scene=" + ScenePath + "; prefab=" + PrefabPath +
                          "; colliders=" + collisions.GetComponentsInChildren<MeshCollider>().Length);
            }
            finally
            {
                if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void AddMarker(Transform parent, string name, Vector3 position)
        {
            var marker = new GameObject(name); marker.transform.SetParent(parent, false);
            marker.transform.localPosition = position;
        }

        private static void MakeFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = path.Substring(0, path.LastIndexOf('/'));
            MakeFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
