#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace TIEC.CurrentMap {
 [InitializeOnLoad] public static class CarMaterialRepair {
  [Serializable] class Fix { public Entry[] materials; }
  [Serializable] class Entry { public string name,texture; public bool double_sided; }
  const string Base="Assets/GroveCurrentMap";
  static double next;
  static CarMaterialRepair(){next=EditorApplication.timeSinceStartup+3;EditorApplication.update+=Wait;}
  static void Wait(){
   if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
   next=EditorApplication.timeSinceStartup+3;
   if(!File.Exists(Base+"/car_material_fix.json")||File.Exists(Base+"/car_material_fix.applied")){EditorApplication.update-=Wait;return;}
   EditorApplication.update-=Wait;
   try {
    var fix=JsonUtility.FromJson<Fix>(File.ReadAllText(Base+"/car_material_fix.json"));
    string backup="MapTransferBackups/CarMaterials_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);
    foreach(var d in fix.materials){
     string path=Base+"/Materials/"+d.name+".mat";
     var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m)throw new Exception("Missing material "+d.name);
     if(!string.IsNullOrEmpty(d.texture)&&!AssetDatabase.LoadAssetAtPath<Texture2D>(Base+"/Textures/"+d.texture))throw new Exception("Missing texture "+d.texture);
     File.Copy(path,backup+"/"+d.name+".mat",true);
    }
    foreach(var d in fix.materials){
     var m=AssetDatabase.LoadAssetAtPath<Material>(Base+"/Materials/"+d.name+".mat");
     m.SetFloat("_Cull",d.double_sided?0:2);m.doubleSidedGI=d.double_sided;
     if(!string.IsNullOrEmpty(d.texture))m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Base+"/Textures/"+d.texture));
     EditorUtility.SetDirty(m);
    }
    AssetDatabase.SaveAssets();SceneView.RepaintAll();
    File.WriteAllText(Base+"/car_material_fix.applied","Corrected original image alpha and Blender two-sided surfaces; materials="+fix.materials.Length+"; backup="+backup);
    Debug.Log("CAR_MATERIAL_REPAIR_OK: "+fix.materials.Length+" materials; backup "+backup);
   } catch(Exception e){File.WriteAllText(Base+"/car_material_fix.error",e.ToString());Debug.LogException(e);}
  }
 }
}
#endif

