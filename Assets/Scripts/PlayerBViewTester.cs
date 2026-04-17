using UnityEngine;
using Meta.XR.MRUtilityKit;

/// <summary>
/// Player B 화면 표시 방식 3가지 프로토타입.
///
/// WallRoom  — Player B의 벽 한 면에 Player A의 방 3D 지오메트리를 직접 부착.
///             렌더 텍스처 없이, 벽 너머로 방이 펼쳐진 것처럼 보이는 "포털 방" 개념.
/// CCTV      — 코너에 설치된 고정 CCTV 카메라 + 별도 모니터 화면
/// Drone     — 방 안을 순찰하는 드론 카메라 + 화면
///
/// 사용법
///   1. 아무 빈 GameObject에 Add Component → PlayerBViewTester
///   2. Inspector에서 ViewMode 선택 후 Play
///   3. 런타임에 SwitchMode() 공개 API로 전환 가능
///
/// Quest 3 / Editor 모두 동작.
/// URP 전용 (Shader: Universal Render Pipeline/Lit, Unlit)
/// </summary>
public class PlayerBViewTester : MonoBehaviour
{
    // ── 공개 열거형 ────────────────────────────────────────────────────────────
    public enum ViewMode { WallRoom, CCTV, Drone }

    // ── Inspector 필드 ────────────────────────────────────────────────────────

    [Header("=== View Mode ===")]
    [Tooltip("테스트할 방식 선택")]
    [SerializeField] private ViewMode viewMode = ViewMode.WallRoom;

    [Header("=== Render Texture (CCTV / Drone 전용) ===")]
    [SerializeField] private int renderWidth  = 1280;
    [SerializeField] private int renderHeight = 720;
    [SerializeField] private float cameraFOV  = 60f;

    [Header("=== Screen (CCTV / Drone 공통) ===")]
    [Tooltip("표시 화면 중심 위치 (월드) — attachScreenToWall 꺼졌을 때만 사용")]
    [SerializeField] private Vector3 screenPosition = new Vector3(0f, 1.4f, 2.4f);
    [Tooltip("표시 화면 회전 (Euler) — attachScreenToWall 꺼졌을 때만 사용")]
    [SerializeField] private Vector3 screenRotation = new Vector3(0f, 0f, 0f);
    [Tooltip("화면 기준 비율 (16:9 권장). 실제 크기는 벽 크기에서 자동 계산됨")]
    [SerializeField] private Vector2 screenSize = new Vector2(1.6f, 0.9f);
    [Tooltip("화면 테두리 두께 (0 이면 테두리 없음)")]
    [SerializeField] private float screenBorderThickness = 0.03f;
    [Tooltip("true: 플레이어(MainCamera) 방향을 보고 Y축을 0/180/-180 중 가장 가까운 값으로 자동 스냅 — attachScreenToWall 꺼졌을 때만 사용")]
    [SerializeField] private bool autoFacePlayer = true;

    [Header("=== Wall Attachment (MRUK) ===")]
    [Tooltip("MRUK 스캔 벽에 화면을 자동으로 붙일지 여부 (에디터/기기 공통)")]
    [SerializeField] private bool attachScreenToWall = true;
    [Tooltip("0 = 가장 큰 벽, 1 = 두 번째로 큰 벽, ...")]
    [SerializeField] private int preferredWallIndex = 0;
    [Tooltip("벽 크기 대비 화면 채움 비율 (0.5 ~ 0.9 권장)")]
    [SerializeField] private float wallScreenFillRatio = 0.75f;
    [Tooltip("벽면에서 얼마나 띄울지 (z-fighting 방지)")]
    [SerializeField] private float wallScreenOffset = 0.015f;
    [Tooltip("화면 중심 높이를 눈높이에 고정할지 여부 (true 권장)")]
    [SerializeField] private bool fixToEyeLevel = true;
    [Tooltip("눈높이 기준 Y값 (m)")]
    [SerializeField] private float eyeLevelHeight = 1.5f;
    [Tooltip("(선택) RoomManager 참조 — 스캔 완료 후 화면 자동 재배치")]
    [SerializeField] private RoomManager roomManager;

    // ── WallRoom 전용 ─────────────────────────────────────────────────────────

    [Header("=== WallRoom 전용 ===")]
    [Tooltip("Player B 방에서 방을 붙일 벽의 중심 위치")]
    [SerializeField] private Vector3 wallCenter = new Vector3(0f, 1.25f, 3f);
    [Tooltip("방이 확장될 방향 (벽 안쪽 방향, 보통 플레이어 반대)")]
    [SerializeField] private Vector3 wallInward = new Vector3(0f, 0f, 1f);
    [Tooltip("부착될 방의 너비 (m)")]
    [SerializeField] private float attachRoomWidth  = 4f;
    [Tooltip("부착될 방의 높이 (m)")]
    [SerializeField] private float attachRoomHeight = 2.5f;
    [Tooltip("부착될 방의 깊이 (m)")]
    [SerializeField] private float attachRoomDepth  = 4f;
    [Tooltip("벽/바닥/천장 색상")]
    [SerializeField] private Color wallRoomWallColor    = new Color(0.85f, 0.82f, 0.78f);
    [SerializeField] private Color wallRoomFloorColor   = new Color(0.55f, 0.45f, 0.35f);
    [SerializeField] private Color wallRoomCeilingColor = new Color(0.92f, 0.92f, 0.90f);
    [Tooltip("더미 가구 배치 여부")]
    [SerializeField] private bool placeDummyFurniture = true;

    // ── CCTV 전용 ─────────────────────────────────────────────────────────────

    [Header("=== CCTV 전용 ===")]
    [Tooltip("CCTV 설치 위치 (천장 근처 코너)")]
    [SerializeField] private Vector3 cctvPosition = new Vector3(2f, 2.6f, 2f);
    [Tooltip("CCTV 카메라 회전 (Euler, 아래 방향)")]
    [SerializeField] private Vector3 cctvRotation = new Vector3(40f, 225f, 0f);
    [Tooltip("CCTV FOV (광각 권장)")]
    [SerializeField] private float cctvFOV = 80f;
    [Tooltip("CCTV 하우징 크기")]
    [SerializeField] private float cctvScale = 0.12f;

    // ── Drone 전용 ────────────────────────────────────────────────────────────

    [Header("=== Drone 전용 ===")]
    [Tooltip("드론 순찰 중심")]
    [SerializeField] private Vector3 droneCenter = new Vector3(0f, 1.8f, 0f);
    [Tooltip("순찰 반지름 (m)")]
    [SerializeField] private float dronePatrolRadius = 1.2f;
    [Tooltip("순찰 속도 (rad/s)")]
    [SerializeField] private float droneAngularSpeed = 0.6f;
    [Tooltip("드론 상하 부유 폭 (m)")]
    [SerializeField] private float droneBobAmplitude = 0.1f;
    [Tooltip("드론 카메라 아래 틸트 각도")]
    [SerializeField] private float droneCamTiltDown = 25f;
    [Tooltip("드론 전체 크기 기준값")]
    [SerializeField] private float droneScale = 0.15f;

    [Header("=== 디버그 ===")]
    [SerializeField] private bool showDebugLog = true;

    // ── 런타임 참조 ───────────────────────────────────────────────────────────

    private Camera        _viewCamera;
    private RenderTexture _renderTexture;
    private GameObject    _screenQuad;
    private GameObject    _screenBorder;
    private GameObject    _cameraRig;
    private GameObject    _droneVisual;
    private GameObject    _cctvVisual;
    private GameObject    _wallRoomRoot;   // WallRoom 모드 전용

    // ══════════════════════════════════════════════════════════════════════════

    void Start()
    {
        if (roomManager != null)
            roomManager.OnRoomReady += HandleRoomReady;

        ApplyMode();
        Log($"초기화 완료 — Mode: {viewMode}");
    }

    void OnDestroy()
    {
        if (roomManager != null)
            roomManager.OnRoomReady -= HandleRoomReady;

        ReleaseRenderTexture();
    }

    // ── 공개 API ──────────────────────────────────────────────────────────────

    /// <summary>런타임에 View Mode를 전환한다.</summary>
    public void SwitchMode(ViewMode newMode)
    {
        // 기존 오브젝트 정리
        if (_wallRoomRoot != null) { Destroy(_wallRoomRoot); _wallRoomRoot = null; }
        if (_droneVisual  != null)
        {
            if (_cameraRig != null) _cameraRig.transform.SetParent(null, true);
            Destroy(_droneVisual);
            _droneVisual = null;
        }
        if (_cctvVisual   != null) { Destroy(_cctvVisual);  _cctvVisual  = null; }
        if (_screenQuad   != null) { Destroy(_screenQuad);  _screenQuad  = null; }
        if (_screenBorder != null) { Destroy(_screenBorder); _screenBorder = null; }
        if (_cameraRig    != null) { Destroy(_cameraRig);   _cameraRig   = null; }
        ReleaseRenderTexture();

        viewMode = newMode;
        ApplyMode();
        Log($"모드 전환 → {newMode}");
    }

    // ── 모드 분기 ─────────────────────────────────────────────────────────────

    private void ApplyMode()
    {
        switch (viewMode)
        {
            case ViewMode.WallRoom: SetupWallRoom(); break;
            case ViewMode.CCTV:    SetupCCTV();     break;
            case ViewMode.Drone:   SetupDrone();    break;
        }
    }

    // ── Mode 1 : WallRoom ─────────────────────────────────────────────────────
    // 렌더 텍스처 / 카메라 없이, 방 3D 지오메트리 자체를 벽면에 부착.
    // Player B가 지정된 벽을 보면 벽 너머로 Player A의 방이 펼쳐진 것처럼 보임.

    private void SetupWallRoom()
    {
        _wallRoomRoot = new GameObject("WallRoom_Root");

        // 좌표계: wallInward 방향이 방 깊이 축(+Z), 월드 Up이 +Y, 그 직교가 +X
        Vector3 fwd   = wallInward.normalized;
        Vector3 up    = Vector3.up;
        Vector3 right = Vector3.Cross(up, fwd).normalized;
        // fwd와 right의 외적으로 실제 up 재계산 (fwd가 기울어진 경우 대비)
        up = Vector3.Cross(fwd, right).normalized;

        float w  = attachRoomWidth;
        float h  = attachRoomHeight;
        float d  = attachRoomDepth;
        float hw = w * 0.5f;
        float hh = h * 0.5f;

        // 벽 부착점(wallCenter)이 방의 "입구 면" 중심 → 방은 fwd 방향으로 d만큼 확장
        // 각 면의 중심 위치 계산
        Vector3 origin = wallCenter; // 입구 면 중심 (Player B 벽)

        // 입구 면(Player B 벽과 맞닿는 면) — 안쪽에서 봐야 하므로 normal = -fwd
        BuildRoomFace("Face_Entrance", origin,
            rotation: Quaternion.LookRotation(-fwd, up),
            width: w, height: h,
            color: wallRoomWallColor);

        // 안쪽 맞은편 벽 (깊이 끝)
        BuildRoomFace("Face_Back", origin + fwd * d,
            rotation: Quaternion.LookRotation(fwd, up),
            width: w, height: h,
            color: wallRoomWallColor);

        // 왼쪽 벽
        BuildRoomFace("Face_Left", origin + fwd * (d * 0.5f) - right * hw,
            rotation: Quaternion.LookRotation(right, up),
            width: d, height: h,
            color: wallRoomWallColor);

        // 오른쪽 벽
        BuildRoomFace("Face_Right", origin + fwd * (d * 0.5f) + right * hw,
            rotation: Quaternion.LookRotation(-right, up),
            width: d, height: h,
            color: wallRoomWallColor);

        // 바닥 (wallCenter 기준 아래 hh)
        BuildRoomFace("Face_Floor", origin + fwd * (d * 0.5f) - up * hh,
            rotation: Quaternion.LookRotation(up, fwd),
            width: w, height: d,
            color: wallRoomFloorColor);

        // 천장 (wallCenter 기준 위 hh)
        BuildRoomFace("Face_Ceiling", origin + fwd * (d * 0.5f) + up * hh,
            rotation: Quaternion.LookRotation(-up, fwd),
            width: w, height: d,
            color: wallRoomCeilingColor);

        if (placeDummyFurniture)
            PlaceDummyFurniture(origin, fwd, right, up, w, h, d);

        Log($"WallRoom 부착 완료 — wallCenter: {wallCenter}, 방 크기: {w}×{h}×{d}m");
    }

    /// <summary>방 한 면(Quad)을 생성하고 _wallRoomRoot에 부착.</summary>
    private void BuildRoomFace(string faceName, Vector3 center, Quaternion rotation,
                                float width, float height, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = faceName;
        go.transform.SetParent(_wallRoomRoot.transform, false);
        go.transform.position  = center;
        go.transform.rotation  = rotation;
        go.transform.localScale = new Vector3(width, height, 1f);
        Destroy(go.GetComponent<MeshCollider>());
        SetPrimitiveColor(go, color);
    }

    /// <summary>간단한 더미 가구(책상, 의자, 책장)를 방 안에 배치.</summary>
    private void PlaceDummyFurniture(Vector3 origin, Vector3 fwd, Vector3 right, Vector3 up,
                                      float w, float h, float d)
    {
        float floorY = origin.y - h * 0.5f; // 바닥 Y

        // ── 책상 (뒷벽 중앙 앞)
        Vector3 deskCenter = origin + fwd * (d * 0.8f) + Vector3.up * (floorY - origin.y + 0.38f);
        SpawnFurnitureBox("Furniture_Desk", deskCenter, new Vector3(1.2f, 0.76f, 0.6f),
            new Color(0.55f, 0.38f, 0.22f));

        // ── 의자 (책상 앞)
        Vector3 chairCenter = origin + fwd * (d * 0.65f) + Vector3.up * (floorY - origin.y + 0.22f);
        SpawnFurnitureBox("Furniture_Chair_Seat", chairCenter, new Vector3(0.5f, 0.44f, 0.5f),
            new Color(0.25f, 0.20f, 0.15f));
        SpawnFurnitureBox("Furniture_Chair_Back",
            chairCenter + fwd * (-0.2f) + Vector3.up * 0.3f,
            new Vector3(0.5f, 0.6f, 0.08f),
            new Color(0.25f, 0.20f, 0.15f));

        // ── 책장 (왼쪽 벽 옆)
        Vector3 shelfBase = origin + fwd * (d * 0.5f) - right * (w * 0.35f)
                          + Vector3.up * (floorY - origin.y + 1.0f);
        SpawnFurnitureBox("Furniture_Shelf", shelfBase, new Vector3(0.4f, 2.0f, 0.3f),
            new Color(0.60f, 0.42f, 0.25f));

        Log("더미 가구 배치 완료");
    }

    private void SpawnFurnitureBox(string objName, Vector3 center, Vector3 size, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = objName;
        go.transform.SetParent(_wallRoomRoot.transform, false);
        go.transform.position   = center;
        go.transform.localScale = size;
        Destroy(go.GetComponent<BoxCollider>());
        SetPrimitiveColor(go, color);
    }

    // ── Mode 2 : CCTV ─────────────────────────────────────────────────────────

    private void SetupCCTV()
    {
        BuildRenderTexture();
        BuildCamera();
        BuildScreen();

        _cameraRig.transform.SetParent(null, false);
        _cameraRig.transform.position = cctvPosition;
        _cameraRig.transform.rotation = Quaternion.Euler(cctvRotation);
        _viewCamera.fieldOfView = cctvFOV;

        // CCTV 하우징
        _cctvVisual = new GameObject("CCTV_Visual");
        _cctvVisual.transform.position = cctvPosition;
        _cctvVisual.transform.rotation = Quaternion.Euler(cctvRotation);

        var body = MakePrimitive("CCTV_Body", PrimitiveType.Cylinder, _cctvVisual.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        body.transform.localScale    = new Vector3(cctvScale, cctvScale * 1.6f, cctvScale);
        SetPrimitiveColor(body, new Color(0.12f, 0.12f, 0.12f));

        var lens = MakePrimitive("CCTV_Lens", PrimitiveType.Sphere, _cctvVisual.transform);
        lens.transform.localPosition = new Vector3(0f, 0f, cctvScale * 1.6f);
        lens.transform.localScale    = Vector3.one * cctvScale * 0.75f;
        SetPrimitiveColor(lens, new Color(0.04f, 0.04f, 0.08f));

        var led = MakePrimitive("CCTV_LED", PrimitiveType.Sphere, _cctvVisual.transform);
        led.transform.localPosition = new Vector3(cctvScale * 0.55f, cctvScale * 0.3f, cctvScale * 1.2f);
        led.transform.localScale    = Vector3.one * cctvScale * 0.22f;
        SetPrimitiveColor(led, new Color(1f, 0.1f, 0.1f), emissive: true);

        var bracket = MakePrimitive("CCTV_Bracket", PrimitiveType.Cube, _cctvVisual.transform);
        bracket.transform.localPosition = new Vector3(0f, cctvScale * 0.85f, 0f);
        bracket.transform.localScale    = new Vector3(cctvScale * 0.4f, cctvScale * 0.8f, cctvScale * 0.4f);
        SetPrimitiveColor(bracket, new Color(0.18f, 0.18f, 0.18f));

        Log($"CCTV 설정 완료 — pos: {cctvPosition}, rot: {cctvRotation}");
    }

    // ── Mode 3 : Drone ────────────────────────────────────────────────────────

    private void SetupDrone()
    {
        BuildRenderTexture();
        BuildCamera();
        BuildScreen();

        _droneVisual = new GameObject("Drone_Visual");
        _droneVisual.transform.position = droneCenter;

        float s = droneScale;

        var body = MakePrimitive("Drone_Body", PrimitiveType.Cube, _droneVisual.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale    = new Vector3(s * 2f, s * 0.5f, s * 2f);
        SetPrimitiveColor(body, new Color(0.18f, 0.18f, 0.22f));

        var dome = MakePrimitive("Drone_CamDome", PrimitiveType.Sphere, _droneVisual.transform);
        dome.transform.localPosition = new Vector3(0f, -s * 0.3f, 0f);
        dome.transform.localScale    = Vector3.one * s * 0.7f;
        SetPrimitiveColor(dome, new Color(0.05f, 0.05f, 0.08f));

        for (int i = 0; i < 4; i++)
        {
            float armAngleDeg = 45f + i * 90f;
            float armAngleRad = armAngleDeg * Mathf.Deg2Rad;
            Vector3 armDir    = new Vector3(Mathf.Cos(armAngleRad), 0f, Mathf.Sin(armAngleRad));

            var arm = MakePrimitive($"Drone_Arm_{i}", PrimitiveType.Cylinder, _droneVisual.transform);
            arm.transform.localPosition = armDir * s * 0.9f;
            arm.transform.localRotation = Quaternion.Euler(0f, -armAngleDeg, 90f);
            arm.transform.localScale    = new Vector3(s * 0.18f, s * 0.9f, s * 0.18f);
            SetPrimitiveColor(arm, new Color(0.25f, 0.25f, 0.3f));

            var rotor = MakePrimitive($"Drone_Rotor_{i}", PrimitiveType.Cylinder, _droneVisual.transform);
            rotor.transform.localPosition = armDir * s * 1.8f + Vector3.up * s * 0.15f;
            rotor.transform.localScale    = new Vector3(s * 1.1f, s * 0.04f, s * 1.1f);
            SetPrimitiveColor(rotor, new Color(0.08f, 0.08f, 0.08f));
        }

        _cameraRig.transform.SetParent(_droneVisual.transform, false);
        _cameraRig.transform.localPosition = new Vector3(0f, -s * 0.5f, s * 0.3f);
        _cameraRig.transform.localRotation = Quaternion.Euler(droneCamTiltDown, 0f, 180f);

        var patrol = _droneVisual.AddComponent<DronePatrol>();
        patrol.Init(droneCenter, dronePatrolRadius, droneAngularSpeed, droneBobAmplitude);

        Log($"Drone 설정 완료 — center: {droneCenter}, r: {dronePatrolRadius}");
    }

    // ── 공통 빌드 (CCTV / Drone 전용) ────────────────────────────────────────

    private void BuildRenderTexture()
    {
        _renderTexture = new RenderTexture(renderWidth, renderHeight, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 2
        };
        _renderTexture.Create();
        Log($"RenderTexture {renderWidth}×{renderHeight} 생성");
    }

    private void BuildCamera()
    {
        _cameraRig = new GameObject("PlayerB_CameraRig");

        var camGO = new GameObject("PlayerB_Camera");
        camGO.transform.SetParent(_cameraRig.transform, false);

        _viewCamera = camGO.AddComponent<Camera>();
        _viewCamera.targetTexture   = _renderTexture;
        _viewCamera.fieldOfView     = cameraFOV;
        _viewCamera.nearClipPlane   = 0.05f;
        _viewCamera.farClipPlane    = 50f;
        _viewCamera.depth           = -10;
        _viewCamera.clearFlags      = CameraClearFlags.SolidColor;
        _viewCamera.backgroundColor = new Color(0.05f, 0.05f, 0.1f);

        Log("PlayerB 카메라 생성");
    }

    private void BuildScreen()
    {
        // MRUK 벽 감지 시도, 실패 시 Inspector 값으로 폴백
        if (!TryGetWallTransform(out Vector3 pos, out Quaternion rot, out Vector2 size))
        {
            pos  = screenPosition;
            rot  = ResolveScreenRotation();
            size = screenSize;
        }

        if (screenBorderThickness > 0f)
        {
            _screenBorder = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _screenBorder.name = "PlayerB_ScreenBorder";
            Destroy(_screenBorder.GetComponent<MeshCollider>());
            _screenBorder.transform.position  = pos;
            _screenBorder.transform.rotation  = rot;
            _screenBorder.transform.localScale = new Vector3(
                size.x + screenBorderThickness * 2f,
                size.y + screenBorderThickness * 2f,
                1f);
            SetPrimitiveColor(_screenBorder, new Color(0.08f, 0.08f, 0.08f));
        }

        _screenQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _screenQuad.name = "PlayerB_Screen";
        Destroy(_screenQuad.GetComponent<MeshCollider>());
        _screenQuad.transform.position  = pos + rot * (Vector3.back * 0.001f);
        _screenQuad.transform.rotation  = rot;
        _screenQuad.transform.localScale = new Vector3(size.x, size.y, 1f);

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            Debug.LogError("[PlayerBViewTester] URP/Unlit 셰이더를 찾지 못했습니다!");
        var mat = new Material(shader != null ? shader : Shader.Find("Standard"));
        if (mat.HasProperty("_BaseMap"))
        {
            mat.SetTexture("_BaseMap", _renderTexture);
            mat.SetTextureScale("_BaseMap",  new Vector2(1f, -1f));
            mat.SetTextureOffset("_BaseMap", new Vector2(0f,  1f));
        }
        else if (mat.HasProperty("_MainTex"))
        {
            mat.SetTexture("_MainTex", _renderTexture);
            mat.SetTextureScale("_MainTex",  new Vector2(1f, -1f));
            mat.SetTextureOffset("_MainTex", new Vector2(0f,  1f));
        }
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", Color.white);
        _screenQuad.GetComponent<Renderer>().material = mat;

        Log("화면 Quad 생성");
    }

    /// <summary>
    /// MRUK에서 지정 인덱스의 벽 앵커를 찾아 화면 위치/회전/크기를 계산.
    /// attachScreenToWall = false 이거나 MRUK 방 데이터 없으면 false 반환.
    /// </summary>
    private bool TryGetWallTransform(out Vector3 pos, out Quaternion rot, out Vector2 size)
    {
        pos  = screenPosition;
        rot  = Quaternion.Euler(screenRotation);
        size = screenSize;

        if (!attachScreenToWall) return false;

        var mruk = MRUK.Instance;
        if (mruk == null) { Log("MRUK 인스턴스 없음 — 폴백 위치 사용"); return false; }

        var room = mruk.GetCurrentRoom();
        if (room == null) { Log("MRUK 방 데이터 없음 — 폴백 위치 사용"); return false; }

        // 벽 앵커 수집 → 면적 내림차순 정렬
        var walls = new System.Collections.Generic.List<MRUKAnchor>();
        foreach (var anchor in room.Anchors)
            if (anchor.HasLabel("WALL_FACE") && anchor.PlaneRect.HasValue)
                walls.Add(anchor);

        if (walls.Count == 0) { Log("WALL_FACE 앵커 없음 — 폴백 위치 사용"); return false; }

        walls.Sort((a, b) =>
        {
            var ra = a.PlaneRect.Value; var rb = b.PlaneRect.Value;
            return (rb.width * rb.height).CompareTo(ra.width * ra.height);
        });

        var wall = walls[Mathf.Clamp(preferredWallIndex, 0, walls.Count - 1)];
        var rect = wall.PlaneRect.Value;

        // 화면 크기: 벽 면적의 fillRatio를 채우되 screenSize 비율(16:9) 유지
        float aspect = screenSize.x / screenSize.y;
        float w = rect.width  * wallScreenFillRatio;
        float h = rect.height * wallScreenFillRatio;
        if (w / h > aspect) w = h * aspect; else h = w / aspect;

        // 위치: 벽 중심에서 방 안쪽으로 wallScreenOffset만큼 띄움
        // (MRUK anchor.forward = 방 바깥 방향이므로 -forward = 방 안쪽)
        pos = wall.transform.position - wall.transform.forward * wallScreenOffset;
        if (fixToEyeLevel) pos.y = eyeLevelHeight;

        // 회전: 화면 법선(+Z)이 방 안쪽을 향하도록
        rot  = Quaternion.LookRotation(-wall.transform.forward, Vector3.up);
        size = new Vector2(w, h);

        Log($"벽 선택: [{wall.name}] 벽={rect.width:F2}×{rect.height:F2}m → 화면={w:F2}×{h:F2}m");
        return true;
    }

    /// <summary>RoomManager 스캔 완료 시 호출 — 화면을 실제 MRUK 벽으로 재배치.</summary>
    private void HandleRoomReady(MRUKRoom _)
    {
        if (viewMode != ViewMode.CCTV && viewMode != ViewMode.Drone) return;
        if (!TryGetWallTransform(out Vector3 pos, out Quaternion rot, out Vector2 size)) return;

        RepositionScreen(pos, rot, size);
        Log("RoomReady → 화면 벽 재배치 완료");
    }

    /// <summary>이미 생성된 화면 Quad/Border를 새 위치로 이동. RoomReady 콜백에서 호출.</summary>
    private void RepositionScreen(Vector3 pos, Quaternion rot, Vector2 size)
    {
        if (_screenQuad != null)
        {
            _screenQuad.transform.position  = pos + rot * (Vector3.back * 0.001f);
            _screenQuad.transform.rotation  = rot;
            _screenQuad.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        if (_screenBorder != null)
        {
            _screenBorder.transform.position  = pos;
            _screenBorder.transform.rotation  = rot;
            _screenBorder.transform.localScale = new Vector3(
                size.x + screenBorderThickness * 2f,
                size.y + screenBorderThickness * 2f,
                1f);
        }
    }

    private void ReleaseRenderTexture()
    {
        if (_renderTexture == null) return;
        _renderTexture.Release();
        Destroy(_renderTexture);
        _renderTexture = null;
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// autoFacePlayer가 켜져 있으면 MainCamera 방향을 기준으로
    /// Y축을 { -180, 0, 180 } 중 가장 가까운 값으로 스냅한 Quaternion을 반환.
    /// 꺼져 있으면 screenRotation을 그대로 반환.
    /// </summary>
    private Quaternion ResolveScreenRotation()
    {
        if (!autoFacePlayer)
            return Quaternion.Euler(screenRotation);

        Camera mainCam = Camera.main;
        if (mainCam == null)
            return Quaternion.Euler(screenRotation);

        // 스크린 → 플레이어 방향 (수평 성분만)
        Vector3 toPlayer = mainCam.transform.position - screenPosition;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.001f)
            return Quaternion.Euler(screenRotation);

        // LookRotation은 -Z가 아니라 +Z를 forward로 쓰므로,
        // "플레이어를 정면으로 보는 Y각도" = toPlayer 방향의 오일러 Y
        float rawY = Quaternion.LookRotation(toPlayer).eulerAngles.y;
        if (rawY > 180f) rawY -= 360f; // -180 ~ 180 범위로 정규화

        // { -180, 0, 180 } 중 DeltaAngle 기준 가장 가까운 값으로 스냅
        float snappedY = 0f;
        float bestDist = float.MaxValue;
        foreach (float candidate in new[] { -180f, 0f, 180f })
        {
            float dist = Mathf.Abs(Mathf.DeltaAngle(rawY, candidate));
            if (dist < bestDist) { bestDist = dist; snappedY = candidate; }
        }

        Log($"Screen Y 자동 스냅: rawY={rawY:F1}° → snappedY={snappedY}°");
        return Quaternion.Euler(screenRotation.x, snappedY, screenRotation.z);
    }

    private static GameObject MakePrimitive(string name, PrimitiveType type, Transform parent)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
        return go;
    }

    private static void SetPrimitiveColor(GameObject go, Color color, bool emissive = false)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return;

        var mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.color = color;

        if (emissive)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 3f);
        }

        renderer.material = mat;
    }

    private void Log(string msg)
    {
        if (showDebugLog) Debug.Log($"[PlayerBViewTester] {msg}");
    }
}

// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// 드론 순찰 이동 컴포넌트.
/// PlayerBViewTester (Drone 모드)가 자동 부착 — 직접 사용 불필요.
/// </summary>
public class DronePatrol : MonoBehaviour
{
    private Vector3 _center;
    private float   _radius;
    private float   _angularSpeed;
    private float   _bobAmplitude;
    private float   _angle;
    private bool    _initialized;

    private readonly Transform[] _rotors = new Transform[4];

    public void Init(Vector3 center, float radius, float angularSpeed, float bobAmplitude)
    {
        _center       = center;
        _radius       = radius;
        _angularSpeed = angularSpeed;
        _bobAmplitude = bobAmplitude;
        _angle        = 0f;
        _initialized  = true;

        for (int i = 0; i < 4; i++)
            _rotors[i] = transform.Find($"Drone_Rotor_{i}");
    }

    void Update()
    {
        if (!_initialized) return;

        _angle += _angularSpeed * Time.deltaTime;

        float x = Mathf.Cos(_angle) * _radius;
        float z = Mathf.Sin(_angle) * _radius;
        float y = _bobAmplitude * Mathf.Sin(Time.time * 2.2f);

        transform.position = _center + new Vector3(x, y, z);

        Vector3 forward = new Vector3(-Mathf.Sin(_angle), 0f, Mathf.Cos(_angle));
        if (forward.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(forward, Vector3.up),
                Time.deltaTime * 4f
            );
        }

        foreach (var rotor in _rotors)
        {
            if (rotor != null)
                rotor.Rotate(Vector3.up, 900f * Time.deltaTime, Space.Self);
        }
    }
}
