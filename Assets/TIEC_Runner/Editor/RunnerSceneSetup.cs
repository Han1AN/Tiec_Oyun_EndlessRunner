#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace TIEC.Runner
{
    public static class RunnerSceneSetup
    {
        public const string GameScene = "Assets/Scenes/GroveRunner_Game.unity";
        const string Folder = "Assets/TIEC_Runner";
        [MenuItem("Tools/TIEC Runner/Build Gameplay Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            var source = SceneManager.GetActiveScene();
            var map = source.GetRootGameObjects().FirstOrDefault(x => x.name.StartsWith("Grove Run Map"));
            var character = source.GetRootGameObjects().FirstOrDefault(x => x.name == "CJ_RidingBMX");
            if (!map || !character) throw new InvalidOperationException("Open Grove_Run_Map with CJ_RidingBMX first.");
            if (source.GetRootGameObjects().Any(x => x.name == "GroveRunner — Gameplay"))
                throw new InvalidOperationException("Gameplay already exists; use Open Gameplay Scene.");
            // Preserve the current map and character, including in-memory edits, in a dedicated gameplay copy.
            EditorSceneManager.SaveScene(source, GameScene, true);
            var scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
            map = scene.GetRootGameObjects().First(x => x.name.StartsWith("Grove Run Map"));
            character = scene.GetRootGameObjects().First(x => x.name == "CJ_RidingBMX");
            int obstacleLayer = EnsureLayer("RunnerObstacle"), groundLayer = EnsureLayer("RunnerGround");
            var config = AssetDatabase.LoadAssetAtPath<RunnerConfig>(Folder + "/RunnerSettings.asset");
            if (!config)
            {
                config = ScriptableObject.CreateInstance<RunnerConfig>();
                AssetDatabase.CreateAsset(config, Folder + "/RunnerSettings.asset");
            }
            config.courseLength = 217; config.obstacleMask = 1 << obstacleLayer; config.groundMask = 1 << groundLayer;
            EditorUtility.SetDirty(config);
            var game = new GameObject("GroveRunner — Gameplay"); Undo.RegisterCreatedObjectUndo(game, "Create runner gameplay");
            var controls = game.AddComponent<RunnerInput>();
            var session = game.AddComponent<RunnerSession>();
            var riderRoot = new GameObject("BicyclePlayer", typeof(Rigidbody), typeof(BoxCollider));
            riderRoot.transform.SetParent(game.transform, false);
            riderRoot.transform.SetPositionAndRotation(new Vector3(0, .08f, -3), Quaternion.Euler(0, 180, 0));
            var pivot = new GameObject("SteeringVisualPivot"); pivot.transform.SetParent(riderRoot.transform, false);
            Undo.SetTransformParent(character.transform, pivot.transform, "Assign bicycle visual");
            character.transform.localPosition = Vector3.zero; character.transform.localRotation = Quaternion.identity;
            character.transform.localScale = Vector3.one;
            var oldBox = character.GetComponent<BoxCollider>();
            var box = riderRoot.GetComponent<BoxCollider>(); box.center = oldBox.center; box.size = oldBox.size;
            oldBox.enabled = false;
            var rb = riderRoot.GetComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var motor = riderRoot.AddComponent<BicycleMotor>();
            var camera = Camera.main;
            if (!camera) throw new InvalidOperationException("MainCamera must be assigned before building.");
            camera.name = "RunnerFollowCamera";
            camera.nearClipPlane = .1f; camera.farClipPlane = 350;
            if (!camera.GetComponent<AudioListener>()) camera.gameObject.AddComponent<AudioListener>();
            var follow = camera.gameObject.AddComponent<RunnerCamera>();
            follow.Configure(riderRoot.transform, session, camera);
            session.Configure(config, controls, motor, follow);
            motor.Configure(config, controls, pivot.transform, character.GetComponent<Animator>(),
                character.transform.Find("Bike/FrontWheel"), character.transform.Find("Bike/RearWheel"),
                character.transform.Find("Bike/Crank"), CreateJumps());
            int hazards = ConfigureCollisions(map.transform, game.transform, obstacleLayer, groundLayer);
            CorrectMarkers(map.transform);
            var bodyFont = CreateFont(Folder + "/Fonts/Roboto-Bold.ttf", "RunnerBody");
            var titleFont = CreateFont(Folder + "/Fonts/UnifrakturCook-Bold.ttf", "RunnerTitle");
            if (titleFont.fallbackFontAssetTable == null) titleFont.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
            if (!titleFont.fallbackFontAssetTable.Contains(bodyFont)) titleFont.fallbackFontAssetTable.Add(bodyFont);
            var hud = RunnerUiFactory.Build(game.transform, session, controls, titleFont, bodyFont);
            controls.Configure(session, hud);
            follow.SnapToTarget();
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 800;
            var buildScenes = EditorBuildSettings.scenes.Where(x => x.path != GameScene).ToList();
            buildScenes.Insert(0, new EditorBuildSettingsScene(GameScene, true)); EditorBuildSettings.scenes = buildScenes.ToArray();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log($"RUNNER_READY scene={GameScene}; hazards={hazards}; duration={config.Duration:F2}; speed={config.SpeedAt(0):F2}→{config.SpeedAt(config.Duration):F2} m/s");
        }
        [MenuItem("Tools/TIEC Runner/Open Gameplay Scene")]
        public static void Open() { EditorSceneManager.OpenScene(GameScene); }
        public static void CompletePartialScene()
        {
            var scene = SceneManager.GetActiveScene();
            var game = scene.GetRootGameObjects().First(x => x.name == "GroveRunner — Gameplay");
            var controls = game.GetComponent<RunnerInput>(); var session = game.GetComponent<RunnerSession>();
            var body = CreateFont(Folder + "/Fonts/Roboto-Bold.ttf", "RunnerBody");
            var title = CreateFont(Folder + "/Fonts/UnifrakturCook-Bold.ttf", "RunnerTitle");
            if (title.fallbackFontAssetTable == null) title.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
            if (!title.fallbackFontAssetTable.Contains(body)) title.fallbackFontAssetTable.Add(body);
            var hud = game.GetComponentInChildren<RunnerHud>(true);
            if (!hud) hud = RunnerUiFactory.Build(game.transform, session, controls, title, body);
            controls.Configure(session, hud);
            Camera.main.GetComponent<RunnerCamera>().SnapToTarget();
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 800;
            var scenes = EditorBuildSettings.scenes.Where(x => x.path != GameScene).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(GameScene, true)); EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name); if (existing >= 0) return existing;
            var obj = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = obj.FindProperty("layers");
            for (int i = 8; i < 32; i++)
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                { layers.GetArrayElementAtIndex(i).stringValue = name; obj.ApplyModifiedProperties(); return i; }
            throw new InvalidOperationException("No free physics layer for " + name);
        }
        static TMP_FontAsset CreateFont(string source, string name)
        {
            string path = Folder + "/Fonts/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path); if (existing) return existing;
            var font = AssetDatabase.LoadAssetAtPath<Font>(source);
            if (!font) throw new InvalidOperationException("Font missing: " + source);
            var asset = TMP_FontAsset.CreateFontAsset(font, 70, 10, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = name; AssetDatabase.CreateAsset(asset, path);
            string chars = string.Concat(Enumerable.Range(32, 352).Select(x => (char)x)) + "←→·";
            asset.TryAddCharacters(chars, out string missing);
            foreach (var atlas in asset.atlasTextures) if (atlas && !AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, asset);
            if (asset.material && !AssetDatabase.Contains(asset.material)) AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset); return asset;
        }
        static int ConfigureCollisions(Transform map, Transform gameplay, int obstacle, int ground)
        {
            int count = 0;
            foreach (var collider in map.GetComponentsInChildren<Collider>(true))
            {
                string name = collider.name;
                if (name.StartsWith("COL_Road_L") || name.StartsWith("COL_Ramp_") || name.StartsWith("COL_Finish_Stopping_Platform"))
                { collider.gameObject.layer = ground; continue; }
                if (name.StartsWith("COL_Vehicle_") || name.StartsWith("COL_Shopfront_Forecourt")) { collider.enabled = false; continue; }
                if (name.Contains("Excavation"))
                {
                    var bounds = collider.bounds;
                    collider.enabled = false;
                    AddHazard(gameplay, name + "_Hole", new Vector3(bounds.center.x, .28f, bounds.center.z),
                        new Vector3(bounds.size.x, .5f, bounds.size.z), obstacle); count++; continue;
                }
                if (name.StartsWith("COL_Work_Crate_74_1"))
                {
                    // Broken imported proxy has no matching visible crate. Only Crate_74_2 exists here.
                    collider.enabled = false; continue;
                }
                if (name.Contains("Sidewalk")) continue;
                if (collider.bounds.max.y > .2f)
                { collider.gameObject.layer = obstacle; if (!collider.GetComponent<RunnerHazard>()) collider.gameObject.AddComponent<RunnerHazard>(); count++; }
            }
            foreach (var renderer in map.GetComponentsInChildren<Renderer>(true))
                if (renderer.name.Contains("Vehicle_") && renderer.name.Contains("obstacle_MODULE") && !renderer.name.StartsWith("COL_"))
                { AddHazard(gameplay, "Corrected_" + renderer.name, renderer.bounds.center, renderer.bounds.size, obstacle); count++; }
            return count;
        }
        static void AddHazard(Transform parent, string name, Vector3 center, Vector3 size, int layer)
        {
            var go = new GameObject(name, typeof(BoxCollider), typeof(RunnerHazard)); go.transform.SetParent(parent, false);
            go.transform.position = center; go.layer = layer; go.GetComponent<BoxCollider>().size = size;
        }
        static void CorrectMarkers(Transform map)
        {
            var markers = map.Find("Map Markers"); if (!markers) return;
            foreach (Transform marker in markers)
            {
                Vector3 p = marker.localPosition; marker.localPosition = new Vector3(-p.x, p.y, -p.z);
            }
            var spawn = markers.Find("Spawn_Player"); if (spawn) spawn.position = new Vector3(0, .08f, -3);
        }
        static RunnerJump[] CreateJumps() => new[]
        {
            new RunnerJump { laneX = 4, takeoffZ = -27, landingZ = -35.5f, takeoffHeight = 1.55f, arcHeight = .8f },
            new RunnerJump { laneX = 4, takeoffZ = -64.5f, landingZ = -72, takeoffHeight = 1.80f, arcHeight = .6f },
            new RunnerJump { laneX = -4, takeoffZ = -85, landingZ = -93.5f, takeoffHeight = 1.70f, arcHeight = .8f },
            new RunnerJump { laneX = 0, takeoffZ = -117.38f, landingZ = -124.5f, takeoffHeight = 2.20f, arcHeight = .6f },
            new RunnerJump { laneX = -4, takeoffZ = -134, landingZ = -142, takeoffHeight = 2.05f, arcHeight = .6f },
            new RunnerJump { laneX = -4, takeoffZ = -160, landingZ = -168.5f, takeoffHeight = 1.45f, arcHeight = .85f },
            new RunnerJump { laneX = 4, takeoffZ = -208, landingZ = -215.5f, takeoffHeight = 1.55f, arcHeight = .7f }
        };
    }
}
#endif
