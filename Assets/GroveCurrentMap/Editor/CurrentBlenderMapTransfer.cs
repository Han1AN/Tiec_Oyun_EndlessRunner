#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace TIEC.CurrentMap
{
 [Serializable] class MaterialData { public string name,texture,normal_texture,metallic_smoothness_texture; public float[] rgba; public float roughness,metallic,emission; public bool alpha_clip,double_sided; }
 [Serializable] class MapData { public string source_sha256,source_snapshot; public MaterialData[] material_data; public TIEC.Runner.RunnerJump[] jumps; }
 [Serializable] class TransferResult { public string source,scene,backup; public int renderers,colliders,truckModels,jumps,missingMaterials,missingScripts; public bool gameplayPreserved; }
 class CurrentMapImport : AssetPostprocessor
 {
  void OnPreprocessModel() {
   if(!assetPath.StartsWith("Assets/GroveCurrentMap/Models/"))return;
   var i=(ModelImporter)assetImporter;i.globalScale=1;i.useFileScale=true;i.importAnimation=false;i.importCameras=false;i.importLights=false;i.addCollider=false;i.meshCompression=ModelImporterMeshCompression.Off;i.importNormals=ModelImporterNormals.Import;i.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;i.materialLocation=ModelImporterMaterialLocation.InPrefab;
  }
  void OnPreprocessTexture() {
   if(!assetPath.StartsWith("Assets/GroveCurrentMap/Textures/"))return;
   var i=(TextureImporter)assetImporter;i.maxTextureSize=2048;i.mipmapEnabled=true;i.alphaSource=TextureImporterAlphaSource.FromInput;
   string p=assetPath.ToLowerInvariant();bool normal=p.Contains("normal");i.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;i.sRGBTexture=!normal&&!p.Contains("metallicsmoothness");i.flipGreenChannel=false;
  }
 }
 [InitializeOnLoad] public static class CurrentBlenderMapTransfer
 {
  const string Base="Assets/GroveCurrentMap",ScenePath="Assets/Scenes/GroveRunner_Game.unity";
  static double next;
  static CurrentBlenderMapTransfer(){next=EditorApplication.timeSinceStartup+4;EditorApplication.update+=Wait;}
  static void Wait() {
   if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
   next=EditorApplication.timeSinceStartup+4;
   if(!File.Exists(Base+"/map_data.json")||!File.Exists(Base+"/apply.request"))return;
   var data=JsonUtility.FromJson<MapData>(File.ReadAllText(Base+"/map_data.json"));
   if(File.Exists(Base+"/applied.sha")&&File.ReadAllText(Base+"/applied.sha")==data.source_sha256){EditorApplication.update-=Wait;return;}
   if(!AssetDatabase.LoadAssetAtPath<GameObject>(Base+"/Models/Grove_Run_Environment.fbx")||!AssetDatabase.LoadAssetAtPath<GameObject>(Base+"/Models/Grove_Run_Collisions.fbx"))return;
   EditorApplication.update-=Wait;
   try{Apply(data);}catch(Exception e){File.WriteAllText(Base+"/transfer_error.txt",e.ToString());Debug.LogException(e);}
  }
  [MenuItem("Tools/TIEC Map/Apply Current Blender Map to Gameplay")]
  public static void ApplyMenu(){Apply(JsonUtility.FromJson<MapData>(File.ReadAllText(Base+"/map_data.json")));}
  static void Apply(MapData data) {
   if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode first.");
   var scene=SceneManager.GetSceneByPath(ScenePath);if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
   string backup="MapTransferBackups/GroveRunner_Game_Before_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity";Directory.CreateDirectory("MapTransferBackups");
   if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Could not save gameplay backup.");
   var roots=scene.GetRootGameObjects();var game=roots.FirstOrDefault(g=>g.name=="GroveRunner — Gameplay");var old=roots.FirstOrDefault(g=>g.name.StartsWith("Grove Run Map"));
   if(!game||!old)throw new InvalidOperationException("Gameplay and original map must exist.");
   int gameplayCount=game.GetComponentsInChildren<Transform>(true).Length;
   var oldColliders=old.GetComponentsInChildren<Collider>(true).GroupBy(c=>c.name).ToDictionary(g=>g.Key,g=>g.First());
   var mats=new Dictionary<string,Material>();if(!AssetDatabase.IsValidFolder(Base+"/Materials"))AssetDatabase.CreateFolder(Base,"Materials");
   foreach(var d in data.material_data) {
    string path=Base+"/Materials/"+d.name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)throw new InvalidOperationException("URP Lit shader missing.");
    if(!m){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}else m.shader=shader;m.name=d.name;
    var color=new Color(d.rgba[0],d.rgba[1],d.rgba[2],d.rgba[3]);m.SetColor("_BaseColor",color);
    var tex=string.IsNullOrEmpty(d.texture)?null:AssetDatabase.LoadAssetAtPath<Texture2D>(Base+"/Textures/"+d.texture);m.SetTexture("_BaseMap",tex);
    m.SetFloat("_Metallic",d.metallic);m.SetFloat("_Smoothness",1-Mathf.Clamp01(d.roughness));
    var normal=string.IsNullOrEmpty(d.normal_texture)?null:AssetDatabase.LoadAssetAtPath<Texture2D>(Base+"/Textures/"+d.normal_texture);m.SetTexture("_BumpMap",normal);if(normal)m.EnableKeyword("_NORMALMAP");else m.DisableKeyword("_NORMALMAP");
    var metal=string.IsNullOrEmpty(d.metallic_smoothness_texture)?null:AssetDatabase.LoadAssetAtPath<Texture2D>(Base+"/Textures/"+d.metallic_smoothness_texture);m.SetTexture("_MetallicGlossMap",metal);if(metal){m.EnableKeyword("_METALLICSPECGLOSSMAP");m.SetFloat("_Smoothness",1);}else m.DisableKeyword("_METALLICSPECGLOSSMAP");
    m.SetFloat("_AlphaClip",d.alpha_clip?1:0);m.SetFloat("_Cutoff",.5f);if(d.alpha_clip){m.EnableKeyword("_ALPHATEST_ON");m.SetOverrideTag("RenderType","TransparentCutout");m.renderQueue=2450;}else{m.DisableKeyword("_ALPHATEST_ON");m.renderQueue=-1;}
    m.SetFloat("_Cull",d.double_sided?0:2);m.doubleSidedGI=d.double_sided;
    if(d.emission>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*d.emission);}
    EditorUtility.SetDirty(m);mats.Add(d.name,m);
   }
   SceneManager.SetActiveScene(scene);
   var previous=roots.FirstOrDefault(g=>g.name=="Grove Current Blender Map");if(previous){previous.name="Archived Previous Blender Map";previous.SetActive(false);}
   var root=new GameObject("Grove Current Blender Map");Undo.RegisterCreatedObjectUndo(root,"Update Blender Map");
   var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Base+"/Models/Grove_Run_Environment.fbx"),scene);visual.name="Current Environment";visual.transform.SetParent(root.transform,false);
   int missing=0;
   foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true)){
    var slots=renderer.sharedMaterials;for(int k=0;k<slots.Length;k++){Material replacement;if(slots[k]&&mats.TryGetValue(slots[k].name,out replacement))slots[k]=replacement;else missing++;}renderer.sharedMaterials=slots;
    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic);
   }
   var collision=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Base+"/Models/Grove_Run_Collisions.fbx"),scene);collision.name="Current Collision Proxies";collision.transform.SetParent(root.transform,false);
   int ground=LayerMask.NameToLayer("RunnerGround"),obstacle=LayerMask.NameToLayer("RunnerObstacle");if(ground<0||obstacle<0)throw new InvalidOperationException("Runner physics layers missing.");
   foreach(var renderer in collision.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
   foreach(var f in collision.GetComponentsInChildren<MeshFilter>(true)){
    if(!f.sharedMesh)continue;var c=f.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;c.convex=false;f.gameObject.isStatic=true;
    Collider original;bool ramp=f.name.StartsWith("COL_RampTruck_");
    if(ramp||f.name.StartsWith("COL_Road_L")||f.name.StartsWith("COL_Finish_Stopping_Platform"))f.gameObject.layer=ground;
    else if(oldColliders.TryGetValue(f.name,out original)){f.gameObject.layer=original.gameObject.layer;c.enabled=original.enabled;}
    else if(f.name.Contains("Excavation")||f.name.StartsWith("COL_Vehicle_")||f.name.StartsWith("COL_Shopfront_Forecourt"))c.enabled=false;
    else if(c.bounds.max.y>.2f)f.gameObject.layer=obstacle;
    if(c.enabled&&f.gameObject.layer==obstacle)f.gameObject.AddComponent<TIEC.Runner.RunnerHazard>();
   }
   // Existing marker references remain intact; only the old art and proxies are hidden.
   foreach(Transform child in old.transform)if(child.name=="Environment"||child.name.StartsWith("Collision Proxies")){Undo.RecordObject(child.gameObject,"Archive prior map geometry");child.gameObject.SetActive(false);}
   var motor=game.GetComponentInChildren<TIEC.Runner.BicycleMotor>(true);if(!motor)throw new InvalidOperationException("BicycleMotor missing.");
   var serialized=new SerializedObject(motor);var jumps=serialized.FindProperty("jumps");jumps.arraySize=data.jumps.Length;
   for(int k=0;k<data.jumps.Length;k++){var v=data.jumps[k];var e=jumps.GetArrayElementAtIndex(k);e.FindPropertyRelative("laneX").floatValue=v.laneX;e.FindPropertyRelative("takeoffZ").floatValue=v.takeoffZ;e.FindPropertyRelative("landingZ").floatValue=v.landingZ;e.FindPropertyRelative("takeoffHeight").floatValue=v.takeoffHeight;e.FindPropertyRelative("arcHeight").floatValue=v.arcHeight;}
   serialized.ApplyModifiedProperties();
   int scripts=0;foreach(var t in root.GetComponentsInChildren<Transform>(true))scripts+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
   int trucks=visual.GetComponentsInChildren<Renderer>(true).Count(x=>x.name.Contains("RampTruck_"));
   var result=new TransferResult{source=data.source_snapshot,scene=ScenePath,backup=backup,renderers=visual.GetComponentsInChildren<Renderer>().Length,colliders=collision.GetComponentsInChildren<MeshCollider>().Length,truckModels=trucks,jumps=data.jumps.Length,missingMaterials=missing,missingScripts=scripts,gameplayPreserved=gameplayCount==game.GetComponentsInChildren<Transform>(true).Length};
   if(missing!=0||scripts!=0||trucks!=7||!result.gameplayPreserved)throw new InvalidOperationException("Map validation failed: "+JsonUtility.ToJson(result));
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
   File.WriteAllText(Base+"/transfer_result.json",JsonUtility.ToJson(result,true));File.WriteAllText(Base+"/applied.sha",data.source_sha256);
   Selection.activeGameObject=root;Debug.Log("CURRENT_BLENDER_MAP_TRANSFERRED: "+JsonUtility.ToJson(result));
  }
 }
}
#endif
