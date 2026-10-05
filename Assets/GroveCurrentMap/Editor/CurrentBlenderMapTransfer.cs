#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json.Linq;
namespace TIEC.CurrentMap
{
 [Serializable] class MaterialData { public string name,texture,normal_texture,metallic_smoothness_texture; public float[] rgba; public float roughness,metallic,emission,normal_strength=1,ambient_emission; public bool alpha_clip,double_sided,transparent; }
 [Serializable] class HoleData { public string name; public float[] center,size; }
 [Serializable] class MarkerData { public string name; public float[] position; }
 [Serializable] class LightingData { public float[] sun_direction,sun_color,ambient_color; public float sun_energy,ambient_intensity; }
 [Serializable] class MapData { public string source_sha256,source_snapshot; public float course_length; public float[] start_unity; public MaterialData[] material_data; public TIEC.Runner.RunnerJump[] jumps; public HoleData[] holes; public MarkerData[] markers; public LightingData lighting; }
 [Serializable] class TransferResult { public string source,scene,backup; public int renderers,colliders,truckModels,jumps,missingMaterials,missingScripts,startBuildings,finalObstacles,holes; public bool gameplayPreserved,emptyStartClear,finalRouteClear; public float courseLength; public Vector3 playerStart; }
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
   if(File.Exists(Base+"/applied.sha")&&File.ReadAllText(Base+"/applied.sha")==data.source_sha256)return;
   if(!AssetDatabase.LoadAssetAtPath<GameObject>(Base+"/Models/Grove_Run_Environment.fbx")||!AssetDatabase.LoadAssetAtPath<GameObject>(Base+"/Models/Grove_Run_Collisions.fbx"))return;
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
    m.SetFloat("_BumpScale",d.normal_strength);
    var metal=string.IsNullOrEmpty(d.metallic_smoothness_texture)?null:AssetDatabase.LoadAssetAtPath<Texture2D>(Base+"/Textures/"+d.metallic_smoothness_texture);m.SetTexture("_MetallicGlossMap",metal);if(metal){m.EnableKeyword("_METALLICSPECGLOSSMAP");m.SetFloat("_Smoothness",1);}else m.DisableKeyword("_METALLICSPECGLOSSMAP");
    m.SetFloat("_AlphaClip",d.alpha_clip?1:0);m.SetFloat("_Cutoff",.5f);if(d.alpha_clip){m.EnableKeyword("_ALPHATEST_ON");m.SetOverrideTag("RenderType","TransparentCutout");m.renderQueue=2450;}else{m.DisableKeyword("_ALPHATEST_ON");m.renderQueue=-1;}
    m.SetFloat("_Cull",d.double_sided?0:2);m.doubleSidedGI=d.double_sided;
    m.SetFloat("_Surface",d.transparent?1:0);m.SetFloat("_Blend",0);m.SetFloat("_SrcBlend",d.transparent?5:1);m.SetFloat("_DstBlend",d.transparent?10:0);m.SetFloat("_ZWrite",d.transparent?0:1);
    if(d.transparent){m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetOverrideTag("RenderType","Transparent");m.renderQueue=3000;}else m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
    float emission=Mathf.Max(d.emission,d.ambient_emission);
    if(emission>0){m.EnableKeyword("_EMISSION");m.SetTexture("_EmissionMap",tex);m.SetColor("_EmissionColor",color*emission);}else{m.DisableKeyword("_EMISSION");m.SetTexture("_EmissionMap",null);m.SetColor("_EmissionColor",Color.black);}
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
    if(ramp||f.name.StartsWith("COL_Road_L")||f.name.StartsWith("COL_StartExtension_Road_L")||f.name.StartsWith("COL_Finish_Stopping_Platform"))f.gameObject.layer=ground;
    else if(oldColliders.TryGetValue(f.name,out original)){f.gameObject.layer=original.gameObject.layer;c.enabled=original.enabled;}
    else if(f.name.Contains("Excavation")||f.name.StartsWith("COL_Vehicle_")||f.name.StartsWith("COL_Shopfront_Forecourt"))c.enabled=false;
    else if(c.bounds.max.y>.2f)f.gameObject.layer=obstacle;
   if(c.enabled&&f.gameObject.layer==obstacle)f.gameObject.AddComponent<TIEC.Runner.RunnerHazard>();
   }
   RepairFrontageColliders(root);
   // Existing marker references remain intact; only the old art and proxies are hidden.
   foreach(Transform child in old.transform)if(child.name=="Environment"||child.name.StartsWith("Collision Proxies")){Undo.RecordObject(child.gameObject,"Archive prior map geometry");child.gameObject.SetActive(false);}
   var motor=game.GetComponentInChildren<TIEC.Runner.BicycleMotor>(true);if(!motor)throw new InvalidOperationException("BicycleMotor missing.");
   var serialized=new SerializedObject(motor);var jumps=serialized.FindProperty("jumps");jumps.arraySize=data.jumps.Length;
   for(int k=0;k<data.jumps.Length;k++){var v=data.jumps[k];var e=jumps.GetArrayElementAtIndex(k);e.FindPropertyRelative("laneX").floatValue=v.laneX;e.FindPropertyRelative("takeoffZ").floatValue=v.takeoffZ;e.FindPropertyRelative("landingZ").floatValue=v.landingZ;e.FindPropertyRelative("takeoffHeight").floatValue=v.takeoffHeight;e.FindPropertyRelative("arcHeight").floatValue=v.arcHeight;}
   var start=new Vector3(data.start_unity[0],data.start_unity[1],data.start_unity[2]);
   serialized.FindProperty("startPosition").vector3Value=start;
   var config=(TIEC.Runner.RunnerConfig)serialized.FindProperty("settings").objectReferenceValue;
   if(!config)throw new InvalidOperationException("Runner settings missing.");
   // Keep the existing acceleration/speed feel when extending the route.
   if(config.courseLength>0)config.targetSeconds*=data.course_length/config.courseLength;
   config.courseLength=data.course_length;EditorUtility.SetDirty(config);
   serialized.ApplyModifiedProperties();
   // Old manually corrected car/hole hazards belong to the retired geometry.
   // Keep the objects for recovery while using the current imported colliders.
   foreach(var hazard in game.GetComponentsInChildren<TIEC.Runner.RunnerHazard>(true))
    if(hazard.name.StartsWith("Corrected_")||hazard.name.EndsWith("_Hole"))hazard.gameObject.SetActive(false);
   if(data.holes!=null)foreach(var h in data.holes){var go=new GameObject(h.name,typeof(BoxCollider),typeof(TIEC.Runner.RunnerHazard));go.transform.SetParent(root.transform,false);go.transform.position=new Vector3(h.center[0],h.center[1],h.center[2]);go.layer=obstacle;go.GetComponent<BoxCollider>().size=new Vector3(h.size[0],h.size[1],h.size[2]);}
   var markersRoot=old.transform.Find("Map Markers");
   if(markersRoot&&data.markers!=null)foreach(var marker in data.markers){var existing=markersRoot.Find(marker.name);if(existing)existing.position=new Vector3(marker.position[0],marker.position[1],marker.position[2]);}
   motor.ResetToStart();foreach(var camera in game.GetComponentsInChildren<TIEC.Runner.RunnerCamera>(true))camera.SnapToTarget();
   if(data.lighting!=null){var l=data.lighting;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(l.ambient_color[0],l.ambient_color[1],l.ambient_color[2]);RenderSettings.ambientIntensity=l.ambient_intensity;RenderSettings.reflectionIntensity=.1f;
    var lights=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>(true)).Where(v=>v.type==LightType.Directional).ToArray();
    foreach(var light in lights){light.color=new Color(l.sun_color[0],l.sun_color[1],l.sun_color[2]);light.intensity=l.sun_energy;light.transform.rotation=Quaternion.LookRotation(new Vector3(l.sun_direction[0],l.sun_direction[1],l.sun_direction[2]),Vector3.up);light.shadowStrength=.65f;}
   }
   Physics.SyncTransforms();
   int scripts=0;foreach(var t in root.GetComponentsInChildren<Transform>(true))scripts+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
   int trucks=visual.GetComponentsInChildren<Renderer>(true).Count(x=>x.name.Contains("RampTruck_"));
   var result=new TransferResult{source=data.source_snapshot,scene=ScenePath,backup=backup,renderers=visual.GetComponentsInChildren<Renderer>().Length,colliders=collision.GetComponentsInChildren<MeshCollider>().Length,truckModels=trucks,jumps=data.jumps.Length,missingMaterials=missing,missingScripts=scripts,gameplayPreserved=gameplayCount==game.GetComponentsInChildren<Transform>(true).Length,
    startBuildings=visual.GetComponentsInChildren<Renderer>().Count(v=>v.name.Contains("StartFrontage")),finalObstacles=collision.GetComponentsInChildren<MeshFilter>().Count(v=>v.name.Contains("FinalChallenge")),holes=data.holes.Length,courseLength=config.courseLength,playerStart=start};
   result.emptyStartClear=CheckEmptyStart(start,config,motor.GetComponent<BoxCollider>());
   result.finalRouteClear=CheckFinalRoute(config,start,motor.GetComponent<BoxCollider>());
   if(missing!=0||scripts!=0||trucks!=7||result.startBuildings<1||result.finalObstacles!=9||!result.gameplayPreserved||!result.emptyStartClear||!result.finalRouteClear)throw new InvalidOperationException("Map validation failed: "+JsonUtility.ToJson(result));
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
   File.WriteAllText(Base+"/transfer_result.json",JsonUtility.ToJson(result,true));File.WriteAllText(Base+"/applied.sha",data.source_sha256);
   Selection.activeGameObject=root;Debug.Log("CURRENT_BLENDER_MAP_TRANSFERRED: "+JsonUtility.ToJson(result));
  }
  // Blender exports frontage collision as world AABBs. Restore the rotated footprint
  // so the boxes do not occupy the empty road corners beside the finish.
  public static int RepairFrontageColliders(GameObject mapRoot) {
   var transforms=(JArray)JObject.Parse(File.ReadAllText(Base+"/map_data.json"))["source_transforms"];
   if(transforms==null)throw new InvalidOperationException("Building source transforms are missing.");
   string folder=Base+"/Collisions";if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder(Base,"Collisions");
   int repaired=0;
   foreach(var collider in mapRoot.GetComponentsInChildren<MeshCollider>(true)) {
    if(collider.name!="COL_Extension_Frontage_1_1"&&collider.name!="COL_Extension_Frontage_-1_1")continue;
    string name=collider.name.Substring(4);
    var source=transforms.FirstOrDefault(t=>(string)t["name"]==name);
    if(source==null)throw new InvalidOperationException("Missing source transform for "+name);
    var matrix=source["matrix_world"];
    float a=(float)matrix[0][0],b=(float)matrix[0][1],c=(float)matrix[1][0],d=(float)matrix[1][1];
    var bounds=collider.bounds;
    float denominator=Mathf.Abs(a*d)-Mathf.Abs(b*c);
    if(Mathf.Abs(denominator)<.0001f)throw new InvalidOperationException("Cannot reconstruct footprint for "+name);
    float ex=(bounds.extents.x*Mathf.Abs(d)-bounds.extents.z*Mathf.Abs(b))/denominator;
    float ez=(bounds.extents.z*Mathf.Abs(a)-bounds.extents.x*Mathf.Abs(c))/denominator;
    if(ex<=0||ez<=0)throw new InvalidOperationException("Invalid reconstructed footprint for "+name);
    var vertices=new Vector3[8];
    for(int i=0;i<4;i++) {
     float u=(i==0||i==3)?-ex:ex,v=i<2?-ez:ez;
     // Blender X/Y correspond to Unity -X/-Z in this imported map.
     Vector3 world=new Vector3(bounds.center.x-a*u-b*v,bounds.min.y,bounds.center.z-c*u-d*v);
     vertices[i]=collider.transform.InverseTransformPoint(world);
     world.y=bounds.max.y;vertices[i+4]=collider.transform.InverseTransformPoint(world);
    }
    int[] triangles={0,1,2,0,2,3,4,6,5,4,7,6,0,4,5,0,5,1,1,5,6,1,6,2,2,6,7,2,7,3,3,7,4,3,4,0};
    // InverseTransformPoint can reverse handedness through a mirrored model parent.
    if((a*d-b*c)*collider.transform.localToWorldMatrix.determinant<0)
     for(int i=0;i<triangles.Length;i+=3){int swap=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=swap;}
    string path=folder+"/"+name+"_Oriented.asset";
    var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
    if(!mesh){mesh=new Mesh{name=name+"_Oriented"};AssetDatabase.CreateAsset(mesh,path);}
    else {Undo.RecordObject(mesh,"Repair finish frontage");mesh.Clear();}
    mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
    Undo.RecordObject(collider,"Repair finish frontage");collider.sharedMesh=null;collider.sharedMesh=mesh;EditorUtility.SetDirty(collider);repaired++;
   }
   return repaired;
  }
  static bool CheckEmptyStart(Vector3 start,TIEC.Runner.RunnerConfig config,BoxCollider box){for(float z=start.z;z>0;z-=.5f){var center=new Vector3(0,.08f,z)+Quaternion.Euler(0,180,0)*box.center;if(Physics.CheckBox(center,box.size*.485f,Quaternion.Euler(0,180,0),config.obstacleMask,QueryTriggerInteraction.Collide))return false;}return true;}
  static bool CheckFinalRoute(TIEC.Runner.RunnerConfig config,Vector3 start,BoxCollider box){
   const float step=.1f;int count=99;var previous=new HashSet<int>(Enumerable.Range(0,count));
   for(float y=220;y<=277.5f;y+=.5f){var next=new HashSet<int>();float speed=config.SpeedAt(config.TimeAtDistance(start.z+y));float movement=config.lateralSpeed*.5f/speed;
    for(int i=0;i<count;i++){float x=-4.9f+i*step;var center=new Vector3(x,.08f,-y)+Quaternion.Euler(0,180,0)*box.center;
     if(Physics.CheckBox(center,box.size*.485f,Quaternion.Euler(0,180,0),config.obstacleMask,QueryTriggerInteraction.Collide))continue;
     foreach(var k in previous){if(Mathf.Abs(i-k)*step>movement)continue;var oldCenter=new Vector3(-4.9f+k*step,.08f,-(y-.5f))+Quaternion.Euler(0,180,0)*box.center;var travel=center-oldCenter;
      if(!Physics.BoxCast(oldCenter,box.size*.485f,travel.normalized,out RaycastHit hit,Quaternion.Euler(0,180,0),travel.magnitude,config.obstacleMask,QueryTriggerInteraction.Collide)){next.Add(i);break;}}
    }if(next.Count==0)return false;previous=next;
   }return true;
  }
 }
}
#endif
// trigger reload 10/05/2026 19:46:52
