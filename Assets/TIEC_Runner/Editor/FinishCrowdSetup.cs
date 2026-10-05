#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TIEC.Runner.Editor
{
    public static class FinishCrowdSetup
    {
        public const string Base = "Assets/TIEC_Runner/FinishCrowd";
        public const string RootName = "CJ Finish — Cheering Friends";
        static readonly string[] Characters = { "BigSmoke", "JerseyFan", "Ryder", "Sweet" };
        static readonly FinishSpectator.CheerStyle[] Styles = {
            FinishSpectator.CheerStyle.Clap, FinishSpectator.CheerStyle.FistPump,
            FinishSpectator.CheerStyle.Wave, FinishSpectator.CheerStyle.Flag };
        static readonly Vector3[] Positions = {
            new Vector3(-2.2f, 0, -283.6f), new Vector3(-.75f, 0, -284.4f),
            new Vector3(.75f, 0, -284.4f), new Vector3(2.2f, 0, -283.6f) };
        static readonly float[] Heights = { 1.9f, 1.8f, 1.83f, 1.85f };

        [MenuItem("Tools/TIEC Runner/Create Finish Cheering Crowd")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/GroveRunner_Game.unity")
                throw new InvalidOperationException("Open GroveRunner_Game before placing the crowd.");
            // Validate all files before replacing an existing crowd.
            foreach (string character in Characters)
            {
                string modelPath = Base + "/Models/" + character + "/rigged.fbx";
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(modelPath))
                    throw new InvalidOperationException("Missing rigged character: " + modelPath);
                if (!AssetDatabase.LoadAssetAtPath<Texture2D>(Base + "/Models/" + character + "/albedo.png"))
                    throw new InvalidOperationException("Missing texture for " + character);
            }
            EnsureFolder(Base + "/Materials");
            EnsureFolder(Base + "/Prefabs");
            var red = Material("RedFlag", new Color(.86f, .018f, .026f), null, true);
            var pole = Material("FlagPole", new Color(.17f, .14f, .1f), null, false);
            var previous = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            if (previous) Undo.DestroyObjectImmediate(previous);
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create cheering crowd");
            SceneManager.MoveGameObjectToScene(root, scene);
            Physics.SyncTransforms();
            int groundMask = LayerMask.GetMask("RunnerGround");
            for (int i = 0; i < Characters.Length; i++)
            {
                string name = Characters[i];
                var fan = new GameObject(name + " — " + Styles[i]);
                fan.transform.SetParent(root.transform, false);
                Vector3 point = Positions[i];
                if (Physics.Raycast(point + Vector3.up * 3f, Vector3.down, out RaycastHit hit,
                    6f, groundMask, QueryTriggerInteraction.Ignore)) point.y = hit.point.y;
                fan.transform.position = point;
                fan.transform.rotation = Quaternion.LookRotation(new Vector3(-point.x * .4f, 0, 5f));
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Base + "/Models/" + name + "/rigged.fbx");
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
                model.name = name + " Meshy rig";
                model.transform.SetParent(fan.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                var renderers = model.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException(name + " has no renderers.");
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                if (bounds.size.y < .01f) throw new InvalidOperationException(name + " has invalid height.");
                float scale = Heights[i] / bounds.size.y;
                model.transform.localScale = Vector3.one * scale;
                // Place the model's lowest vertex at the surface regardless of the FBX origin.
                model.transform.localPosition = Vector3.up * ((point.y - bounds.min.y) * scale);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Base + "/Models/" + name + "/albedo.png");
                var material = Material(name, Color.white, texture, false);
                foreach (var renderer in renderers)
                {
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                    if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, 0);
                }
                foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                var animator = model.GetComponentInChildren<Animator>(true);
                if (!animator) animator = model.AddComponent<Animator>();
                var spectator = fan.AddComponent<FinishSpectator>();
                spectator.speed = .75f + i * .11f;
                spectator.Configure(animator, Styles[i], i * .217f, red, pole);
                if (!spectator.HasArmRig) throw new InvalidOperationException(name + " arm rig was not recognized.");
                // Save bind poses; generated poses are applied during play or explicit previews.
                spectator.RestorePose();
            }
            var fillObject = new GameObject("Finish Crowd Soft Light");
            fillObject.transform.SetParent(root.transform, false);
            fillObject.transform.position = new Vector3(0, 3f, -280.8f);
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(1f, .9f, .78f);
            fill.range = 7f;
            fill.intensity = 4f;
            fill.shadows = LightShadows.None;
            DartTargetRemoval.Apply(GameObject.Find("Grove Current Blender Map"));
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, Base + "/Prefabs/FinishCheeringCrowd.prefab",
                InteractionMode.AutomatedAction);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
            Selection.activeGameObject = root;
            Debug.Log("FINISH_CROWD_READY: Four Meshy characters, separate cheering loops and one red flag.");
        }

        static Material Material(string name, Color color, Texture texture, bool doubleSided)
        {
            string path = Base + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", .08f);
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Cull", doubleSided ? 0 : 2);
            material.doubleSidedGI = doubleSided;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            string parent = path.Substring(0, split);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(split + 1));
        }
    }
}
#endif
