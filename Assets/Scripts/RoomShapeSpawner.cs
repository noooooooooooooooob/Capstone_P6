using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;

/// <summary>
/// Quest 3 방 스캔(MRUK) 결과를 직접 구독하여,
/// 방 크기와 가구(침대 등) 유무에 따라 기하학적 도형을 공중에 띄우는 스크립트.
///
/// ※ RoomManager에 의존하지 않음 — MRUK 이벤트를 직접 수신.
/// ※ [BuildingBlock] EffectMesh가 방 메시를 담당하므로, 이 스크립트는 도형 스폰만 처리.
/// ※ 생성된 도형은 [BuildingBlock] Cube 패턴을 따름:
///    Rigidbody(Kinematic) + Collider + Grabbable(Oculus.Interaction)
///    → 손으로 잡고 이동 가능
///
/// [동작 규칙]
///   방 바닥 면적 기준:
///     작은 방 (< 10㎡)   → 정사면체(Tetrahedron) × 3
///     중간 방 (10~20㎡)  → 구(Sphere) × 4
///     큰 방   (> 20㎡)   → 정육면체(Cube) × 5
///   침대(BED) 감지 시    → 침대 위에 캡슐(Capsule) 추가
///
/// [설치]
///   아무 GameObject에 Add Component → RoomShapeSpawner
///   MRUK Building Block이 씬에 있으면 자동 동작.
/// </summary>
public class RoomShapeSpawner : MonoBehaviour
{
    [Header("Shape Settings")]
    [Tooltip("도형 크기 (반지름)")]
    [SerializeField] private float shapeRadius = 0.15f;

    [Tooltip("도형이 떠 있는 높이 (바닥 기준, 미터)")]
    [SerializeField] private float floatHeight = 1.8f;

    [Tooltip("도형 배치 원의 반지름")]
    [SerializeField] private float orbitRadius = 1.0f;

    [Tooltip("침대 위 캡슐 높이 (침대 상면 기준)")]
    [SerializeField] private float bedCapsuleHeight = 1.5f;

    [Header("Materials (비워두면 URP Lit 자동 생성)")]
    [SerializeField] private Material shapeMaterial;
    [SerializeField] private Material bedCapsuleMaterial;

    [Header("Floating Animation")]
    [SerializeField] private float bobAmplitude = 0.15f;
    [SerializeField] private float bobSpeed = 1.5f;
    [SerializeField] private float spinSpeed = 30f;

    [Header("Grabbable (Building Blocks 패턴)")]
    [Tooltip("true면 손으로 잡을 수 있는 Grabbable 컴포넌트 부착")]
    [SerializeField] private bool makeGrabbable = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = true;

    private readonly List<GameObject> _spawnedShapes = new();
    private bool _processed;

    // ── 생명주기 ─────────────────────────────────────────────────────────────

    void Start()
    {
        // MRUK 이벤트 직접 구독 — RoomManager 불필요
        StartCoroutine(WaitForMRUK());
    }

    void OnDestroy()
    {
        // MRUK 이벤트 해제
        if (MRUK.Instance != null)
        {
            MRUK.Instance.RoomCreatedEvent.RemoveListener(OnMRUKRoomCreated);
        }
    }

    /// <summary>
    /// MRUK 싱글톤이 초기화될 때까지 대기 후 이벤트 구독.
    /// EffectMesh Building Block이 MRUK를 초기화하므로 약간의 대기가 필요할 수 있음.
    /// </summary>
    private IEnumerator WaitForMRUK()
    {
        Log("MRUK 인스턴스 대기 중...");

        // MRUK 싱글톤이 준비될 때까지 대기 (최대 10초)
        float timeout = 10f;
        float elapsed = 0f;
        while (MRUK.Instance == null && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (MRUK.Instance == null)
        {
            Log("❌ MRUK 인스턴스를 찾을 수 없습니다! MRUK Building Block이 씬에 있는지 확인하세요.", true);
            yield break;
        }

        Log("✅ MRUK 인스턴스 발견");

        // RoomCreatedEvent 구독 (방이 새로 생성/로드될 때 발동)
        MRUK.Instance.RoomCreatedEvent.AddListener(OnMRUKRoomCreated);

        // 이미 방 데이터가 로드되어 있는 경우 (EffectMesh가 먼저 로드했을 수 있음)
        var existingRoom = MRUK.Instance.GetCurrentRoom();
        if (existingRoom != null)
        {
            Log("이미 로드된 방 데이터 발견 → 즉시 처리");
            ProcessRoom(existingRoom);
        }
        else
        {
            Log("방 데이터 대기 중... (스캔 완료 시 자동 처리)");

            // 방 데이터가 아직 없으면 폴링으로 추가 대기 (최대 60초)
            float roomTimeout = 60f;
            float roomElapsed = 0f;
            while (!_processed && roomElapsed < roomTimeout)
            {
                roomElapsed += 1f;
                yield return new WaitForSeconds(1f);

                var room = MRUK.Instance.GetCurrentRoom();
                if (room != null)
                {
                    Log($"폴링으로 방 데이터 감지 ({roomElapsed:F0}초 경과)");
                    ProcessRoom(room);
                    yield break;
                }
            }

            if (!_processed)
            {
                Log("⚠️ 60초 내에 방 데이터를 받지 못했습니다.", true);
            }
        }
    }

    /// <summary>
    /// MRUK RoomCreatedEvent 핸들러.
    /// </summary>
    private void OnMRUKRoomCreated(MRUKRoom room)
    {
        Log("MRUK RoomCreatedEvent 수신!");
        if (!_processed)
        {
            ProcessRoom(room);
        }
    }

    // ── 방 분석 ──────────────────────────────────────────────────────────────

    private struct RoomAnalysis
    {
        public float floorArea;
        public float floorWidth;
        public float floorDepth;
        public int bedCount;
        public Vector3 roomCenter;
        public float floorY;
        public List<MRUKAnchor> bedAnchors;
        public List<string> allLabels;
    }

    private RoomAnalysis AnalyzeRoom(MRUKRoom room)
    {
        var result = new RoomAnalysis
        {
            bedAnchors = new List<MRUKAnchor>(),
            allLabels = new List<string>()
        };

        Log("── 방 앵커 전체 목록 ──");
        int idx = 0;
        foreach (var anchor in room.Anchors)
        {
            string labels = string.Join(", ", anchor.AnchorLabels);
            bool hasPlane = anchor.PlaneRect.HasValue;
            bool hasVolume = anchor.VolumeBounds.HasValue;
            string planeInfo = hasPlane ? $"Plane({anchor.PlaneRect.Value.width:F2}×{anchor.PlaneRect.Value.height:F2})" : "no-plane";
            string volInfo = hasVolume ? $"Vol({anchor.VolumeBounds.Value.size.x:F2}×{anchor.VolumeBounds.Value.size.y:F2}×{anchor.VolumeBounds.Value.size.z:F2})" : "no-vol";
            Log($"  [{idx}] {labels} | {planeInfo} | {volInfo} | pos={anchor.transform.position}");
            result.allLabels.Add($"{labels} [{planeInfo}, {volInfo}]");
            idx++;
        }

        // ── 바닥 ──
        var floor = room.FloorAnchor;
        if (floor != null && floor.PlaneRect.HasValue)
        {
            var rect = floor.PlaneRect.Value;
            result.floorWidth = rect.width;
            result.floorDepth = rect.height;
            result.floorArea = rect.width * rect.height;
            result.roomCenter = floor.transform.position;
            result.floorY = floor.transform.position.y;
        }
        else
        {
            // 바닥 없으면 전체 앵커 위치 평균
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var anchor in room.Anchors)
            {
                sum += anchor.transform.position;
                count++;
            }
            result.roomCenter = count > 0 ? sum / count : Camera.main.transform.position;
            result.floorArea = 15f;
            result.floorY = result.roomCenter.y;
            Log("⚠️ 바닥(FLOOR) 앵커 없음 — 기본값 15㎡, 앵커 평균 위치 사용");
        }

        // ── 가구 탐색 ──
        foreach (var anchor in room.Anchors)
        {
            if (anchor.HasLabel("BED"))
            {
                result.bedCount++;
                result.bedAnchors.Add(anchor);
            }
        }

        return result;
    }

    // ── 도형 스폰 ────────────────────────────────────────────────────────────

    private void ProcessRoom(MRUKRoom room)
    {
        if (_processed) return;
        _processed = true;

        ClearShapes();
        var analysis = AnalyzeRoom(room);

        // ── 분석 결과 요약 ──
        Log("═══════════════════════════════════════════");
        Log($"  앵커 총 수: {room.Anchors.Count}");
        Log($"  바닥 크기: {analysis.floorWidth:F2}m × {analysis.floorDepth:F2}m");
        Log($"  바닥 면적: {analysis.floorArea:F1}㎡");
        Log($"  방 중심: {analysis.roomCenter}");
        Log($"  바닥 Y: {analysis.floorY:F2}m");
        Log($"  침대(BED) 수: {analysis.bedCount}");
        Log("═══════════════════════════════════════════");

        // ── 1. 방 크기 → 도형 결정 ──
        ShapeType shapeType;
        int shapeCount;
        Color shapeColor;
        string sizeCategory;

        if (analysis.floorArea < 10f)
        {
            shapeType = ShapeType.Tetrahedron;
            shapeCount = 3;
            shapeColor = new Color(1f, 0.3f, 0.2f, 1f);
            sizeCategory = "작은 방";
        }
        else if (analysis.floorArea < 20f)
        {
            shapeType = ShapeType.Sphere;
            shapeCount = 4;
            shapeColor = new Color(0.2f, 0.6f, 1f, 1f);
            sizeCategory = "중간 방";
        }
        else
        {
            shapeType = ShapeType.Cube;
            shapeCount = 5;
            shapeColor = new Color(0.2f, 1f, 0.4f, 1f);
            sizeCategory = "큰 방";
        }

        Log($"→ 판정: {sizeCategory} ({analysis.floorArea:F1}㎡) → {shapeType} × {shapeCount}개");

        // 방 중앙 상공에 원형 배치
        float spawnY = analysis.floorY + floatHeight;
        for (int i = 0; i < shapeCount; i++)
        {
            float angle = (360f / shapeCount) * i * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * orbitRadius,
                0f,
                Mathf.Sin(angle) * orbitRadius
            );
            Vector3 spawnPos = analysis.roomCenter + offset;
            spawnPos.y = spawnY;

            GameObject shape = CreateShape(shapeType, spawnPos, shapeColor, $"RoomShape_{shapeType}_{i}");
            var motion = shape.AddComponent<FloatingMotion>();
            motion.Init(bobAmplitude, bobSpeed, spinSpeed, i * (360f / shapeCount));
            _spawnedShapes.Add(shape);
        }

        // ── 2. 침대 감지 → 캡슐 ──
        if (analysis.bedCount > 0)
        {
            Color capsuleColor = new Color(1f, 0.85f, 0.1f, 1f);
            Log($"→ 침대 {analysis.bedCount}개 → 금색 캡슐 스폰");

            for (int i = 0; i < analysis.bedAnchors.Count; i++)
            {
                var bedAnchor = analysis.bedAnchors[i];
                Vector3 bedPos = bedAnchor.transform.position;
                float bedTopY = bedPos.y;

                if (bedAnchor.VolumeBounds.HasValue)
                {
                    bedTopY += bedAnchor.VolumeBounds.Value.extents.y;
                    var sz = bedAnchor.VolumeBounds.Value.size;
                    Log($"  침대 #{i}: pos={bedPos}, size={sz.x:F2}×{sz.y:F2}×{sz.z:F2}m");
                }

                Vector3 capsulePos = new Vector3(bedPos.x, bedTopY + bedCapsuleHeight, bedPos.z);
                GameObject capsule = CreateShape(ShapeType.Capsule, capsulePos, capsuleColor, $"BedCapsule_{i}");
                var motion = capsule.AddComponent<FloatingMotion>();
                motion.Init(bobAmplitude * 1.5f, bobSpeed * 0.8f, spinSpeed * 0.5f, 0f);
                _spawnedShapes.Add(capsule);
            }
        }
        else
        {
            Log("→ 침대 미감지 → 캡슐 없음");
        }

        Log($"✅ 도형 스폰 완료! 총 {_spawnedShapes.Count}개");
    }

    // ── 도형 생성 팩토리 ─────────────────────────────────────────────────────

    private enum ShapeType { Tetrahedron, Sphere, Cube, Capsule }

    private GameObject CreateShape(ShapeType type, Vector3 position, Color color, string name)
    {
        GameObject go;
        Material mat = GetOrCreateMaterial(color, type == ShapeType.Capsule);

        switch (type)
        {
            case ShapeType.Tetrahedron:
                go = ProceduralMeshHelper.CreateTetrahedronObject(name, shapeRadius, mat);
                // Tetrahedron은 MeshCollider 필요 (프리미티브 콜라이더 없음)
                var mc = go.AddComponent<MeshCollider>();
                mc.convex = true;
                mc.isTrigger = true;
                break;

            case ShapeType.Sphere:
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = name;
                go.transform.localScale = Vector3.one * shapeRadius * 2f;
                go.GetComponent<Renderer>().sharedMaterial = mat;
                // SphereCollider 이미 있음 — trigger로 변경
                go.GetComponent<Collider>().isTrigger = true;
                break;

            case ShapeType.Cube:
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.localScale = Vector3.one * shapeRadius * 1.8f;
                go.GetComponent<Renderer>().sharedMaterial = mat;
                go.GetComponent<Collider>().isTrigger = true;
                break;

            case ShapeType.Capsule:
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = name;
                go.transform.localScale = Vector3.one * shapeRadius * 1.5f;
                go.GetComponent<Renderer>().sharedMaterial = mat;
                go.GetComponent<Collider>().isTrigger = true;
                break;

            default:
                go = new GameObject(name);
                break;
        }

        go.transform.position = position;

        // ── Building Blocks 패턴: Grabbable 오브젝트로 설정 ──
        // [BuildingBlock] Cube와 동일한 컴포넌트 구성
        if (makeGrabbable)
        {
            SetupGrabbable(go);
        }

        return go;
    }

    /// <summary>
    /// [BuildingBlock] Cube 패턴을 따라 Grabbable 컴포넌트 설정.
    /// Rigidbody(Kinematic, No Gravity) + Oculus.Interaction.Grabbable
    /// → 손/컨트롤러로 잡고 이동 가능.
    /// Oculus.Interaction이 없으면 Rigidbody만 붙이고 경고 로그.
    /// </summary>
    private void SetupGrabbable(GameObject go)
    {
        // 1. Rigidbody — [BuildingBlock] Cube와 동일 설정
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

        // 2. Grabbable — Oculus.Interaction 네임스페이스
        // 런타임에 타입을 찾아서 부착 (컴파일 의존성 없이)
        var grabbableType = System.Type.GetType("Oculus.Interaction.Grabbable, Oculus.Interaction.Runtime");
        if (grabbableType == null)
        {
            // 패키지 이름이 다를 수 있으므로 Assembly 탐색
            grabbableType = FindTypeInAssemblies("Oculus.Interaction.Grabbable");
        }

        if (grabbableType != null)
        {
            var grabbable = go.AddComponent(grabbableType);

            // Grabbable._rigidbody 필드에 rb 할당 (리플렉션)
            var rbField = grabbableType.GetField("_rigidbody",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (rbField != null)
            {
                rbField.SetValue(grabbable, rb);
            }

            // _targetTransform 설정
            var targetField = grabbableType.GetField("_targetTransform",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (targetField != null)
            {
                targetField.SetValue(grabbable, go.transform);
            }

            // _kinematicWhileSelected = true, _throwWhenUnselected = true
            var kinField = grabbableType.GetField("_kinematicWhileSelected",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (kinField != null) kinField.SetValue(grabbable, true);

            var throwField = grabbableType.GetField("_throwWhenUnselected",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (throwField != null) throwField.SetValue(grabbable, true);

            Log($"  Grabbable 설정 완료: {go.name}");
        }
        else
        {
            Log($"  ⚠️ Oculus.Interaction.Grabbable 타입을 찾을 수 없음. Rigidbody만 부착: {go.name}");
        }
    }

    /// <summary>로드된 어셈블리에서 타입 이름으로 검색.</summary>
    private static System.Type FindTypeInAssemblies(string fullName)
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }

    private Material GetOrCreateMaterial(Color color, bool isCapsule)
    {
        Material baseMat = isCapsule ? bedCapsuleMaterial : shapeMaterial;
        if (baseMat != null)
        {
            var mat = new Material(baseMat);
            mat.SetColor("_BaseColor", color);
            return mat;
        }

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null) shader = Shader.Find("Hidden/Universal Render Pipeline/FallbackError");

        var newMat = new Material(shader);
        newMat.SetColor("_BaseColor", color);
        newMat.SetFloat("_Metallic", 0.3f);
        newMat.SetFloat("_Smoothness", 0.8f);
        return newMat;
    }

    // ── 공개 API ─────────────────────────────────────────────────────────────

    /// <summary>스폰된 도형 모두 제거 후 재처리 가능하도록 리셋.</summary>
    public void ResetAndRespawn()
    {
        _processed = false;
        ClearShapes();

        var room = MRUK.Instance?.GetCurrentRoom();
        if (room != null)
        {
            ProcessRoom(room);
        }
    }

    public void ClearShapes()
    {
        foreach (var go in _spawnedShapes)
            if (go != null) Destroy(go);
        _spawnedShapes.Clear();
    }

    private void Log(string msg, bool isError = false)
    {
        if (!showDebugLog && !isError) return;
        if (isError) Debug.LogError($"[ShapeSpawner] {msg}");
        else         Debug.Log($"[ShapeSpawner] {msg}");
    }
}

// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// 오브젝트를 공중에서 둥둥 부유 + Y축 회전 애니메이션.
/// RoomShapeSpawner가 자동 부착.
/// </summary>
public class FloatingMotion : MonoBehaviour
{
    private float _amplitude = 0.15f;
    private float _speed     = 1.5f;
    private float _spinSpeed = 30f;
    private float _phaseOffset;
    private Vector3 _startPos;
    private bool _initialized;

    public void Init(float amplitude, float speed, float spinSpeed, float phaseOffsetDeg)
    {
        _amplitude   = amplitude;
        _speed       = speed;
        _spinSpeed   = spinSpeed;
        _phaseOffset = phaseOffsetDeg * Mathf.Deg2Rad;
        _startPos    = transform.position;
        _initialized = true;
    }

    void Start()
    {
        if (!_initialized)
            _startPos = transform.position;
    }

    void Update()
    {
        float y = Mathf.Sin(Time.time * _speed + _phaseOffset) * _amplitude;
        transform.position = _startPos + Vector3.up * y;
        transform.Rotate(Vector3.up, _spinSpeed * Time.deltaTime, Space.World);
    }
}
