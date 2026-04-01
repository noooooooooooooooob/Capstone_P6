using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Meta Scene API로 실제 방을 스캔하고 가상 메시로 시각화하는 단독 테스트 스크립트.
/// OVRSceneManager 컴포넌트와 함께 동일한 GameObject에 부착할 것.
/// Link / Editor 환경에서는 Dummy Room 모드로 자동 전환됨.
/// </summary>
[RequireComponent(typeof(OVRSceneManager))]
public class RoomBuildTest : MonoBehaviour
{
    [Header("Surface Materials (없으면 기본 색상 자동 생성)")]
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Material floorMaterial;
    [SerializeField] private Material ceilingMaterial;
    [SerializeField] private Material furnitureMaterial;

    [Header("Dummy Room (Link / Editor 테스트용)")]
    [SerializeField] private bool forceDummyRoom = false;
    [SerializeField] private float roomWidth  = 4f;
    [SerializeField] private float roomDepth  = 5f;
    [SerializeField] private float roomHeight = 2.5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = true;

    private OVRSceneManager _sceneManager;
    private readonly List<GameObject> _spawnedObjects = new();

    private static readonly Dictionary<string, Color> LabelColors = new()
    {
        { OVRSceneManager.Classification.WallFace, new Color(0.6f, 0.8f, 1.0f, 0.5f) },
        { OVRSceneManager.Classification.Floor,    new Color(0.5f, 1.0f, 0.5f, 0.5f) },
        { OVRSceneManager.Classification.Ceiling,  new Color(1.0f, 1.0f, 0.6f, 0.5f) },
    };

    void Awake()
    {
        Debug.Log("[RoomBuildTest] Awake called.");
        _sceneManager = GetComponent<OVRSceneManager>();
        if (_sceneManager == null)
        {
            Debug.LogError("[RoomBuildTest] OVRSceneManager component not found!");
            return;
        }
        _sceneManager.SceneModelLoadedSuccessfully += OnSceneLoaded;
        _sceneManager.NoSceneModelToLoad += OnNoSceneModel;
        Debug.Log("[RoomBuildTest] Listening for scene load events...");
    }

    void Start()
    {
        // Link / Editor 환경이거나 forceDummyRoom이 켜져 있으면 바로 더미 방 생성
        bool isEditor = Application.isEditor;
        if (forceDummyRoom || isEditor)
        {
            Log($"Dummy Room 모드 시작 (isEditor={isEditor}, forceDummy={forceDummyRoom})");
            ClearSpawnedObjects();
            BuildDummyRoom();
        }
    }

    void OnDestroy()
    {
        if (_sceneManager == null) return;
        _sceneManager.SceneModelLoadedSuccessfully -= OnSceneLoaded;
        _sceneManager.NoSceneModelToLoad -= OnNoSceneModel;
    }

    // ── Real Scene API ────────────────────────────────────────────────────────

    private void OnSceneLoaded()
    {
        Log("Scene model loaded. Building room visuals...");
        ClearSpawnedObjects();
        BuildRoomVisuals();
    }

    private void OnNoSceneModel()
    {
        Log("No scene model found. Quest 설정 > Space Setup을 먼저 실행하세요.", isWarning: true);
    }

    private void BuildRoomVisuals()
    {
        var anchors = FindObjectsByType<OVRSceneAnchor>(FindObjectsSortMode.None);
        Log($"Found {anchors.Length} scene anchors.");

        foreach (var anchor in anchors)
        {
            var classification = anchor.GetComponent<OVRSemanticClassification>();
            string label = (classification != null && classification.Labels.Count > 0)
                ? classification.Labels[0]
                : "unknown";

            Material mat = ResolveMaterial(label);

            var plane = anchor.GetComponent<OVRScenePlane>();
            if (plane != null)
            {
                SpawnPlaneVisual(anchor.transform, plane.Width, plane.Height, label, mat);
                continue;
            }

            var volume = anchor.GetComponent<OVRSceneVolume>();
            if (volume != null)
                SpawnVolumeVisual(anchor.transform, volume.Width, volume.Height, volume.Depth, label, mat);
        }

        Log($"Room build complete. Spawned {_spawnedObjects.Count} objects.");
    }

    // ── Dummy Room ────────────────────────────────────────────────────────────

    /// <summary>
    /// Scene API 없이 roomWidth x roomDepth x roomHeight 크기의 더미 방을 생성.
    /// 바닥/천장/4면 벽을 Quad로 배치.
    /// </summary>
    private void BuildDummyRoom()
    {
        float w = roomWidth;
        float d = roomDepth;
        float h = roomHeight;
        float hw = w * 0.5f;
        float hd = d * 0.5f;
        float hh = h * 0.5f;

        Material wallMat    = ResolveMaterial(OVRSceneManager.Classification.WallFace);
        Material floorMat   = ResolveMaterial(OVRSceneManager.Classification.Floor);
        Material ceilingMat = ResolveMaterial(OVRSceneManager.Classification.Ceiling);

        // 바닥
        SpawnDummyQuad("Floor",   new Vector3(0, 0, 0),      Quaternion.Euler(90, 0, 0),   w, d, floorMat);
        // 천장
        SpawnDummyQuad("Ceiling", new Vector3(0, h, 0),      Quaternion.Euler(-90, 0, 0),  w, d, ceilingMat);
        // 앞벽
        SpawnDummyQuad("Wall_F",  new Vector3(0, hh, hd),    Quaternion.Euler(0, 180, 0),  w, h, wallMat);
        // 뒷벽
        SpawnDummyQuad("Wall_B",  new Vector3(0, hh, -hd),   Quaternion.identity,          w, h, wallMat);
        // 왼쪽 벽
        SpawnDummyQuad("Wall_L",  new Vector3(-hw, hh, 0),   Quaternion.Euler(0, 90, 0),   d, h, wallMat);
        // 오른쪽 벽
        SpawnDummyQuad("Wall_R",  new Vector3(hw, hh, 0),    Quaternion.Euler(0, -90, 0),  d, h, wallMat);

        Log($"Dummy room built: {w}m x {d}m x {h}m ({_spawnedObjects.Count} surfaces)");
    }

    private void SpawnDummyQuad(string label, Vector3 pos, Quaternion rot, float width, float height, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = $"DummyRoom_{label}";
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale    = new Vector3(width, height, 1f);
        Destroy(go.GetComponent<MeshCollider>());
        go.GetComponent<Renderer>().sharedMaterial = mat;
        _spawnedObjects.Add(go);
    }

    // ── Shared Helpers ────────────────────────────────────────────────────────

    private void SpawnPlaneVisual(Transform parent, float width, float height, string label, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = $"RoomPlane_{label}";
        go.transform.SetParent(parent, false);
        go.transform.localScale = new Vector3(width, height, 1f);
        Destroy(go.GetComponent<MeshCollider>());
        go.GetComponent<Renderer>().sharedMaterial = mat;
        _spawnedObjects.Add(go);
        Log($"  Plane [{label}] {width:F2} x {height:F2} m");
    }

    private void SpawnVolumeVisual(Transform parent, float w, float h, float d, string label, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = $"RoomVolume_{label}";
        go.transform.SetParent(parent, false);
        go.transform.localScale = new Vector3(w, h, d);
        Destroy(go.GetComponent<BoxCollider>());
        go.GetComponent<Renderer>().sharedMaterial = mat;
        _spawnedObjects.Add(go);
        Log($"  Volume [{label}] {w:F2} x {h:F2} x {d:F2} m");
    }

    private void ClearSpawnedObjects()
    {
        foreach (var go in _spawnedObjects)
            if (go != null) Destroy(go);
        _spawnedObjects.Clear();
    }

    private Material ResolveMaterial(string label)
    {
        Material assigned = label switch
        {
            OVRSceneManager.Classification.Floor    => floorMaterial,
            OVRSceneManager.Classification.Ceiling  => ceilingMaterial,
            OVRSceneManager.Classification.WallFace => wallMaterial,
            _                                        => furnitureMaterial,
        };
        if (assigned != null) return assigned;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        Color color = LabelColors.TryGetValue(label, out var c) ? c : new Color(1f, 0.5f, 0f, 0.5f);
        mat.color = color;
        mat.SetFloat("_Surface", 1);
        mat.SetFloat("_Blend", 0);
        mat.renderQueue = 3000;
        return mat;
    }

    private void Log(string msg, bool isWarning = false)
    {
        if (!showDebugLog) return;
        if (isWarning) Debug.LogWarning($"[RoomBuildTest] {msg}");
        else Debug.Log($"[RoomBuildTest] {msg}");
    }
}
