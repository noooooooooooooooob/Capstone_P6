using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Android;
using Meta.XR.MRUtilityKit;
using TMPro;

/// <summary>
/// Player A의 실제 방을 MRUK로 스캔하고, Player B가 보게 될 가상 방을 재구성하는 스크립트.
///
/// [실행 흐름]
///   실기기(Quest) : MRUK로 실제 방 스캔 → JSON 저장 → 방 메시 재구성 → 게임 오브젝트 배치
///   에디터        : forceDummyRoom 체크 시 더미 방 생성 (크기 직접 지정 가능)
///                   MRUK Inspector에서 Data Source = Prefab 설정 시 내장 샘플 방 사용 가능
/// </summary>
public class RoomManager : MonoBehaviour
{
    // 각 표면 타입별 머티리얼. 할당하지 않으면 fallbackMaterial을 기반으로 색상만 바꿔서 자동 생성.
    [Header("Surface Materials")]
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Material floorMaterial;
    [SerializeField] private Material ceilingMaterial;
    [SerializeField] private Material furnitureMaterial;
    [SerializeField] private Material fallbackMaterial; // 위 머티리얼이 없을 때 사용할 기본 URP 머티리얼

    // 프리팹이 없으면 색상 큐브로 대체해서 배치됨
    [Header("Virtual Objects")]
    [SerializeField] private GameObject puzzlePrefab;
    [SerializeField] private GameObject portalPrefab;
    [SerializeField] private GameObject terminalPrefab;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI statusText;

    // forceDummyRoom = true이면 실기기에서도 더미 방으로 강제 실행 (테스트용)
    [Header("Dummy Room")]
    [SerializeField] private bool forceDummyRoom = false;
    [SerializeField] private float roomWidth  = 4f;
    [SerializeField] private float roomDepth  = 5f;
    [SerializeField] private float roomHeight = 2.5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = true;

    private MRUK _mruk;
    private MRUKRoom _room;
    private readonly List<GameObject> _spawned = new(); // 생성된 오브젝트 목록 (ResetRoom 시 일괄 삭제용)

    // MRUK 앵커 레이블별 기본 색상 (머티리얼 미할당 시 사용)
    private static readonly Dictionary<string, Color> LabelColors = new()
    {
        { "WALL_FACE", new Color(1.0f, 1.0f, 1.0f) },
        { "FLOOR",     new Color(1.0f, 1.0f, 1.0f) },
        { "CEILING",   new Color(1.0f, 1.0f, 1.0f) },
    };

    void Start()
    {
        _mruk = MRUK.Instance;

        if (forceDummyRoom)
        {
            // 테스트용 더미 방: roomWidth/Depth/Height 값으로 박스형 방을 직접 생성
            SetStatus("Starting dummy room...");
            BuildDummyRoom();
            SetStatus($"✅ Dummy room ready! ({_spawned.Count} objects)");
        }
        else
        {
            // 실기기: MRUK로 실제 방 스캔 시작
            // 에디터: MRUK Inspector의 Data Source 설정에 따라 샘플 방 or 실기기 데이터 사용
            SetStatus("Starting room scan...");
            StartCoroutine(RunScan());
        }
    }

    // ── JSON 저장 ─────────────────────────────────────────────────────────────
    // 스캔된 방 데이터를 JSON으로 저장. Quest에서 USB 연결 후 파일 탐색기로 가져올 수 있음.
    // 경로: 내 PC > [Quest 기기] > Internal shared storage > Oculus > VideoShots > room.json

    private void SaveRoomJson()
    {
        if (_mruk == null) return;

        // MRUK가 현재 씬 데이터를 JSON 문자열로 직렬화
        string json = _mruk.SaveSceneToJsonString(false, null);
        string path = "/sdcard/Oculus/VideoShots/room.json";
        try
        {
            File.WriteAllText(path, json);
            Log($"Room data saved: {path}");
            SetStatus($"✅ Scan complete! (saved)");
        }
        catch (System.Exception e)
        {
            Log($"Save failed: {e.Message}", true);
            SetStatus("⚠️ Save failed (check log)");
        }
    }

    // ── 스캔 파이프라인 ───────────────────────────────────────────────────────

    private IEnumerator RunScan()
    {
        // MRUK 싱글톤이 씬에 존재하는지 확인 (MRUK Building Block이 있어야 함)
        _mruk = MRUK.Instance;
        if (_mruk == null)
        {
            SetStatus("❌ MRUK instance not found\nCheck that MRUK Building Block is in the Scene");
            yield break;
        }

        // 실기기에서는 실제 방을 스캔하고, 에디터에서는 MRUK Inspector 설정(Data Source)에 따라 동작
        SetStatus("Scanning room...");
        yield return _mruk.LoadSceneFromDevice();

        // 스캔 결과로 현재 방 앵커 데이터를 가져옴
        _room = _mruk.GetCurrentRoom();
        if (_room == null)
        {
            SetStatus("❌ Scan failed\nQuest Settings > Physical Space > Space Setup");
            yield break;
        }

        // 스캔 성공 시 JSON으로 저장 (실기기 전용, 에디터에서는 경로 오류 발생 가능)
        SetStatus("✅ Scan complete!");
        SaveRoomJson();
        yield return new WaitForSeconds(0.5f);

        // 스캔된 앵커 데이터를 기반으로 방 표면(벽/바닥/천장)을 Quad 메시로 재구성
        // → 이것이 Player B가 보게 될 방의 시각적 표현
        SetStatus("Rebuilding room mesh...");
        BuildRoomVisuals();
        yield return null;

        // 재구성된 방 안에 퍼즐, 포탈, 터미널 오브젝트 배치
        SetStatus("Placing game objects...");
        yield return PlaceGameObjects();

        SetStatus($"✅ Done! {_spawned.Count} objects spawned");
    }

    // ── 방 시각화 ─────────────────────────────────────────────────────────────

    // MRUK 앵커의 기존 렌더러를 숨김 (우리가 직접 재구성한 메시와 중복 렌더링 방지)
    private void HideMRUKRenderers()
    {
        foreach (var anchor in _room.Anchors)
            foreach (var r in anchor.GetComponentsInChildren<Renderer>())
                r.enabled = false;
    }

    // MRUK 앵커 데이터(위치/크기/레이블)를 읽어 벽/바닥/천장 표면을 Quad로 재구성.
    // 가구(BED, TABLE 등 Volume 앵커)는 제외 — 방 구조만 재현.
    // MRUK 앵커의 forward(+Z)는 방 바깥을 향하므로, Quad를 180도 뒤집어 안쪽을 바라보게 함.
    private void BuildRoomVisuals()
    {
        Log($"Total anchors: {_room.Anchors.Count}");
        foreach (var anchor in _room.Anchors)
            Log($"  Anchor: {anchor.name} | PlaneRect={anchor.PlaneRect.HasValue} | VolumeBounds={anchor.VolumeBounds.HasValue} | Labels={string.Join(",", anchor.AnchorLabels)}");

        int surfaceCount = 0;
        foreach (var anchor in _room.Anchors)
        {
            // PlaneRect가 없는 앵커는 Volume(가구 등) → 스킵
            if (!anchor.PlaneRect.HasValue) continue;

            // 벽/바닥/천장 레이블만 처리
            bool isFloor   = anchor.HasLabel("FLOOR");
            bool isCeiling = anchor.HasLabel("CEILING");
            bool isWall    = anchor.HasLabel("WALL_FACE");
            if (!isFloor && !isCeiling && !isWall) continue;

            Material mat = ResolveMaterial(anchor);
            var rect = anchor.PlaneRect.Value;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = $"Room_{anchor.name}";
            Destroy(go.GetComponent<MeshCollider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;

            // 앵커의 월드 위치/회전을 그대로 사용하되,
            // MRUK 앵커 forward가 방 바깥을 향하므로 Y축 180도 회전으로 안쪽을 바라보게 뒤집음
            go.transform.position = anchor.transform.position;
            go.transform.rotation = anchor.transform.rotation * Quaternion.Euler(0, 180, 0);
            go.transform.localScale = new Vector3(rect.width, rect.height, 1f);

            _spawned.Add(go);
            Log($"  [{anchor.name}] pos={anchor.transform.position} size={rect.width:F2}x{rect.height:F2}");
            surfaceCount++;
        }

        Log($"Room visualization complete: {surfaceCount} surfaces");
    }

    // ── 게임 오브젝트 배치 ────────────────────────────────────────────────────

    private IEnumerator PlaceGameObjects()
    {
        PlacePortalAtFloor();
        yield return null;

        PlacePuzzlesOnWalls();
        yield return null;

        PlaceTerminalAtCamera();
    }

    // 바닥 앵커 중심에 포탈 배치 (바닥에서 2cm 위로 올려 겹침 방지)
    private void PlacePortalAtFloor()
    {
        var floor = _room.FloorAnchor;
        if (floor == null) return;

        var pos = floor.transform.position;
        pos.y += 0.02f;

        var go = SpawnGameObject(portalPrefab, pos, Quaternion.identity, "Portal", Color.cyan);
        go.transform.localScale = Vector3.one * 0.5f;
    }

    // 벽 앵커마다 퍼즐 오브젝트 배치 (최대 4개, 눈높이 1.2m)
    private void PlacePuzzlesOnWalls()
    {
        int count = 0;
        foreach (var anchor in _room.Anchors)
        {
            if (!anchor.HasLabel("WALL_FACE")) continue;
            if (count >= 4) break;

            // 벽 표면에서 10cm 앞으로 띄워서 z-fighting 방지
            var pos = anchor.transform.position + anchor.transform.forward * 0.1f;
            pos.y = GetFloorY() + 1.2f;

            // 퍼즐이 벽을 바라보도록 앵커 forward의 반대 방향으로 회전
            SpawnGameObject(puzzlePrefab, pos,
                Quaternion.LookRotation(-anchor.transform.forward),
                $"Puzzle_{count}", Color.yellow);
            count++;
        }
    }

    // 카메라(플레이어 시점) 정면 1.5m 앞에 터미널 배치
    private void PlaceTerminalAtCamera()
    {
        var cam = Camera.main.transform;
        var pos = cam.position + cam.forward * 1.5f;
        pos.y = GetFloorY() + 1.0f;

        SpawnGameObject(terminalPrefab, pos,
            Quaternion.LookRotation(cam.forward),
            "Terminal", Color.green);
    }

    // 바닥 앵커의 Y 좌표를 반환. 앵커가 없으면 0을 기본값으로 사용.
    private float GetFloorY()
    {
        var floor = _room.FloorAnchor;
        return floor != null ? floor.transform.position.y : 0f;
    }

    // ── 더미 방 ───────────────────────────────────────────────────────────────
    // 실제 스캔 없이 Inspector에서 지정한 크기로 박스형 방을 생성. 에디터 테스트용.

    private void BuildDummyRoom()
    {
        float w = roomWidth, d = roomDepth, h = roomHeight;
        float hw = w * 0.5f, hd = d * 0.5f, hh = h * 0.5f;

        Material wallMat    = wallMaterial    ?? CreateColorMaterial("WALL_FACE");
        Material floorMat   = floorMaterial   ?? CreateColorMaterial("FLOOR");
        Material ceilingMat = ceilingMaterial ?? CreateColorMaterial("CEILING");

        // 바닥/천장은 수평 Quad, 4면 벽은 수직 Quad로 생성
        SpawnDummyQuad("Floor",   new Vector3(0, 0, 0),    Quaternion.Euler(90, 0, 0),   w, d, floorMat);
        SpawnDummyQuad("Ceiling", new Vector3(0, h, 0),    Quaternion.Euler(-90, 0, 0),  w, d, ceilingMat);
        SpawnDummyQuad("Wall_F",  new Vector3(0, hh,  hd), Quaternion.Euler(0, 180, 0),  w, h, wallMat);
        SpawnDummyQuad("Wall_B",  new Vector3(0, hh, -hd), Quaternion.identity,          w, h, wallMat);
        SpawnDummyQuad("Wall_L",  new Vector3(-hw, hh, 0), Quaternion.Euler(0,  90, 0),  d, h, wallMat);
        SpawnDummyQuad("Wall_R",  new Vector3( hw, hh, 0), Quaternion.Euler(0, -90, 0),  d, h, wallMat);

        Log($"Dummy room created: {w}m x {d}m x {h}m ({_spawned.Count} surfaces)");
    }

    private void SpawnDummyQuad(string label, Vector3 pos, Quaternion rot,
                                float width, float height, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = $"DummyRoom_{label}";
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale    = new Vector3(width, height, 1f);
        Destroy(go.GetComponent<MeshCollider>());
        go.GetComponent<Renderer>().sharedMaterial = mat;
        _spawned.Add(go);
    }

    // ── 공통 헬퍼 ────────────────────────────────────────────────────────────

    // 프리팹이 있으면 인스턴스화, 없으면 색상 큐브로 대체 생성
    private GameObject SpawnGameObject(GameObject prefab, Vector3 pos, Quaternion rot,
                                       string label, Color fallbackColor)
    {
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab, pos, rot);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = Vector3.one * 0.3f;
            go.GetComponent<Renderer>().material.color = fallbackColor;
        }

        go.name = label;
        _spawned.Add(go);
        Log($"[Object] {label} -> {pos}");
        return go;
    }

    // 앵커 레이블에 따라 Inspector 머티리얼 반환. 없으면 색상 머티리얼 자동 생성.
    private Material ResolveMaterial(MRUKAnchor anchor)
    {
        if (anchor.HasLabel("FLOOR"))     return floorMaterial    ?? CreateColorMaterial("FLOOR");
        if (anchor.HasLabel("CEILING"))   return ceilingMaterial  ?? CreateColorMaterial("CEILING");
        if (anchor.HasLabel("WALL_FACE")) return wallMaterial     ?? CreateColorMaterial("WALL_FACE");
        return furnitureMaterial ?? CreateColorMaterial("OTHER");
    }

    // fallbackMaterial을 복제해 색상만 바꿔서 반환.
    // fallbackMaterial도 없으면 경고 로그 출력 후 에러 셰이더 머티리얼 반환 (분홍색으로 표시됨).
    private Material CreateColorMaterial(string label)
    {
        Material baseMat = fallbackMaterial ?? wallMaterial ?? floorMaterial ?? ceilingMaterial;
        if (baseMat == null)
        {
            Log("No fallback material assigned. Please assign any URP material in the Inspector.", true);
            return new Material(Shader.Find("Hidden/Universal Render Pipeline/FallbackError"));
        }
        var mat = new Material(baseMat);
        Color color = LabelColors.TryGetValue(label, out var c) ? c : new Color(1f, 0.5f, 0f);
        mat.SetColor("_BaseColor", color);
        return mat;
    }

    private void SetStatus(string msg)
    {
        Log(msg);
        if (statusText != null) statusText.text = msg;
    }

    private void Log(string msg, bool isWarning = false)
    {
        if (!showDebugLog) return;
        if (isWarning) Debug.LogWarning($"[RoomManager] {msg}");
        else           Debug.Log($"[RoomManager] {msg}");
    }
}
