using UnityEngine;

namespace TIEC.Runner
{
    /// <summary>A lightweight hand-following pole and a curved, fluttering red cloth.</summary>
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(110)]
    public sealed class WavingFlag : MonoBehaviour
    {
        public Transform hand;
        public Transform poseSpace;
        public Material clothMaterial;
        public Material poleMaterial;
        [Range(3, 32)] public int columns = 12;
        [Range(3, 24)] public int rows = 8;
        public float poleLength = 1.22f;
        public float clothWidth = 0.74f;
        public float clothHeight = 0.47f;
        [Range(0f, 0.4f)] public float flutter = 0.13f;
        [Range(0.2f, 2f)] public float speed = 0.85f;
        [Range(0f, 1f)] public float phase;

        [SerializeField, HideInInspector] MeshFilter clothFilter;
        [SerializeField, HideInInspector] MeshRenderer clothRenderer;
        [SerializeField, HideInInspector] Transform pole;
        Mesh clothMesh;
        Vector3[] vertices;
        bool initialized;

        public void Configure(Transform grip, Transform frame, Material redCloth,
            Material poleSurface, float size = 1f, float startPhase = 0f, float tempo = 0.85f)
        {
            hand = grip;
            poseSpace = frame;
            clothMaterial = redCloth;
            poleMaterial = poleSurface;
            phase = Mathf.Repeat(startPhase, 1f);
            speed = tempo;
            // Keep geometry in spectator units, rather than inheriting the FBX hand's scale.
            transform.localScale = Vector3.one * Mathf.Max(0.05f, size);
            BuildMesh();
        }

        // Rebuild the transient mesh after a scene reload, including in authoring previews.
        void OnEnable() => BuildMesh();
        void LateUpdate() { if (Application.isPlaying) ApplyPose(Time.time); }

        public void ApplyPose(float time)
        {
            if (float.IsNaN(time) || float.IsInfinity(time)) return;
            if (!initialized) BuildMesh();
            float t = (time * Mathf.Clamp(speed, 0.2f, 2f) + phase) * Mathf.PI * 2f;
            if (hand && poseSpace)
            {
                transform.position = hand.position;
                transform.rotation = poseSpace.rotation * Quaternion.Euler(
                    12f * Mathf.Sin(t + 0.7f), 8f * Mathf.Sin(t * 0.7f),
                    -28f * Mathf.Sin(t));
            }
            if (!clothMesh || vertices == null) return;
            int widthCount = Mathf.Clamp(columns, 3, 32), heightCount = Mathf.Clamp(rows, 3, 24);
            if (vertices.Length != widthCount * heightCount) { BuildMesh(); return; }
            for (int y = 0; y < heightCount; y++)
            {
                float v = y / (float)(heightCount - 1);
                for (int x = 0; x < widthCount; x++)
                {
                    float u = x / (float)(widthCount - 1);
                    float travel = t * 3.1f - u * 6.8f;
                    float fold = flutter * u * (Mathf.Sin(travel + v * 1.8f)
                        + 0.3f * Mathf.Sin(travel * 1.8f - v * 3f));
                    vertices[y * widthCount + x] = new Vector3(u * clothWidth,
                        poleLength - 0.15f - clothHeight + v * clothHeight
                        - 0.07f * u * u + 0.018f * u * Mathf.Sin(travel), fold);
                }
            }
            clothMesh.vertices = vertices;
            clothMesh.RecalculateNormals();
            clothMesh.RecalculateBounds();
        }

        public void BuildMesh()
        {
            if (!clothFilter)
            {
                var cloth = new GameObject("Red cloth");
                cloth.transform.SetParent(transform, false);
                clothFilter = cloth.AddComponent<MeshFilter>();
                clothRenderer = cloth.AddComponent<MeshRenderer>();
            }
            if (!clothRenderer) clothRenderer = clothFilter.GetComponent<MeshRenderer>();
            if (clothMaterial) clothRenderer.sharedMaterial = clothMaterial;
            // GPU Resident Drawer excludes renderers with per-instance properties.
            // Keep this changing mesh on the standard rendering path across reloads.
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", clothMaterial ? clothMaterial.GetColor("_BaseColor") : Color.red);
            clothRenderer.SetPropertyBlock(properties);
            if (!pole)
            {
                var poleObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                poleObject.name = "Flag pole";
                poleObject.transform.SetParent(transform, false);
                pole = poleObject.transform;
                Collider collider = poleObject.GetComponent<Collider>();
                if (collider)
                {
                    if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
                }
            }
            pole.localPosition = new Vector3(0f, poleLength * 0.5f - 0.15f, 0f);
            pole.localRotation = Quaternion.identity;
            pole.localScale = new Vector3(0.018f, poleLength * 0.5f, 0.018f);
            if (poleMaterial) pole.GetComponent<MeshRenderer>().sharedMaterial = poleMaterial;
            ReleaseMesh();
            int widthCount = Mathf.Clamp(columns, 3, 32), heightCount = Mathf.Clamp(rows, 3, 24);
            vertices = new Vector3[widthCount * heightCount];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[(widthCount - 1) * (heightCount - 1) * 6];
            int index = 0;
            for (int y = 0; y < heightCount; y++)
                for (int x = 0; x < widthCount; x++)
                {
                    int a = y * widthCount + x;
                    uv[a] = new Vector2(x / (float)(widthCount - 1), y / (float)(heightCount - 1));
                    if (x == widthCount - 1 || y == heightCount - 1) continue;
                    triangles[index++] = a;
                    triangles[index++] = a + widthCount;
                    triangles[index++] = a + 1;
                    triangles[index++] = a + 1;
                    triangles[index++] = a + widthCount;
                    triangles[index++] = a + widthCount + 1;
                }
            clothMesh = new Mesh { name = "Procedural red cheering flag", hideFlags = HideFlags.DontSave };
            clothMesh.MarkDynamic();
            clothMesh.vertices = vertices;
            clothMesh.uv = uv;
            clothMesh.triangles = triangles;
            clothFilter.sharedMesh = clothMesh;
            initialized = true;
            ApplyPose(0f);
        }

        void OnDestroy() => ReleaseMesh();
        void ReleaseMesh()
        {
            if (!clothMesh) return;
            if (clothFilter && clothFilter.sharedMesh == clothMesh) clothFilter.sharedMesh = null;
            if (Application.isPlaying) Destroy(clothMesh); else DestroyImmediate(clothMesh);
            clothMesh = null;
        }
    }
}
