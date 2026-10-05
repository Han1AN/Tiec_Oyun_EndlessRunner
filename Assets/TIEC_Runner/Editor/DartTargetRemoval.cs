#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TIEC.Runner.Editor
{
    public static class DartTargetRemoval
    {
        // The current FBX combines the pole, red rings and white rings into three meshes.
        // Keep the stopping platform and finish banner independent of these visual overrides.
        public static int Apply(GameObject mapRoot)
        {
            if (mapRoot == null) return 0;
            int hidden = 0;
            foreach (var renderer in mapRoot.GetComponentsInChildren<Renderer>(true))
            {
                string name = renderer.name;
                bool namedTarget = name.StartsWith("Finish_Target_");
                bool mergedTarget = name == "Final_079_05_Ramps_and_Obstacles"
                    || name == "Final_080_05_Ramps_and_Obstacles"
                    || name == "Final_081_05_Ramps_and_Obstacles";
                if (!namedTarget && !mergedTarget) continue;
                Vector3 center = renderer.bounds.center;
                if (mergedTarget && (Mathf.Abs(center.x) > .5f || center.z > -280f || center.z < -284f))
                    continue;
                if (renderer.gameObject.activeSelf)
                {
                    Undo.RecordObject(renderer.gameObject, "Remove finish dart target");
                    renderer.gameObject.SetActive(false);
                    EditorUtility.SetDirty(renderer.gameObject);
                }
                hidden++;
            }
            return hidden;
        }
    }
}
#endif
