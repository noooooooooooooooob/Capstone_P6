#if UNITY_EDITOR
using Capstone.Puzzle;
using Fusion;
using UnityEditor;
using UnityEngine;

namespace Capstone.EditorTools
{
    /// <summary>
    /// 선택된 라디에이터 GameObject 아래에 배관(파이프)과 밸브 어셈블리를 절차적으로 생성.
    /// 메뉴: Tools / Capstone / Build Radiator Pipes And Valve
    ///
    /// 동작:
    ///  1) 라디에이터의 MeshFilter bounds로 크기를 추정
    ///  2) 우측 하단에 수직 파이프 + 밖으로 튀어나오는 짧은 스텁 파이프 생성
    ///  3) 스텁 끝에 Valve(NetworkObject + RadiatorValve) 배치
    ///  4) Valve의 자식으로 ValveHandle(휠) 생성, 콜라이더와 ValveRotationGrab 부착
    /// </summary>
    public static class BuildRadiatorPipesAndValve
    {
        const string MenuPath = "Tools/Capstone/Build Radiator Pipes And Valve";

        const float PipeRadius   = 0.04f;
        const float StubLength   = 0.30f;
        const float WheelRadius  = 0.18f;

        [MenuItem(MenuPath, true)]
        static bool Validate() => Selection.activeGameObject != null;

        [MenuItem(MenuPath)]
        static void Build()
        {
            GameObject radiator = Selection.activeGameObject;
            if (radiator == null)
            {
                Debug.LogError("[Capstone] Radiator GameObject를 먼저 선택하세요.");
                return;
            }

            Undo.SetCurrentGroupName("Build Radiator Pipes And Valve");
            int undoGroup = Undo.GetCurrentGroup();

            Bounds bounds = EstimateLocalBounds(radiator);
            float halfX = Mathf.Max(0.1f, bounds.extents.x);
            float halfY = Mathf.Max(0.1f, bounds.extents.y);

            // === 1. Pipes 컨테이너 ============================================
            var pipesGo = CreateChild(radiator.transform, "Pipes");

            // === 2. 수직 파이프 (라디에이터 우측 하단에서 바닥 근처까지) ==========
            float xRight = halfX * 0.9f;
            float pipeYCenter = -halfY * 0.2f;
            float verticalLen = halfY * 1.4f;

            CreatePipe(pipesGo.transform, "Pipe_Vertical",
                position: new Vector3(xRight, pipeYCenter, 0f),
                rotationEuler: Vector3.zero,
                radius: PipeRadius,
                length: verticalLen);

            // === 3. 스텁 파이프 (수직 파이프에서 +Z 방향으로 튀어나옴) ============
            float stubY = -halfY * 0.05f;
            CreatePipe(pipesGo.transform, "Pipe_Stub",
                position: new Vector3(xRight, stubY, StubLength * 0.5f),
                rotationEuler: new Vector3(90f, 0f, 0f),
                radius: PipeRadius,
                length: StubLength);

            // === 4. Valve 루트 (NetworkObject + RadiatorValve) =================
            var valveGo = CreateChild(radiator.transform, "Valve");
            valveGo.transform.localPosition = new Vector3(xRight, stubY, StubLength + 0.02f);
            valveGo.transform.localRotation = Quaternion.identity;

            // 4-1. 밸브 허브(스텁 끝의 작은 캡)
            CreatePipe(valveGo.transform, "ValveHub",
                position: Vector3.zero,
                rotationEuler: new Vector3(90f, 0f, 0f),
                radius: PipeRadius * 1.6f,
                length: 0.06f);

            // === 5. ValveHandle (실제로 회전하는 휠) ===========================
            var handleGo = new GameObject("ValveHandle");
            Undo.RegisterCreatedObjectUndo(handleGo, "Create ValveHandle");
            handleGo.transform.SetParent(valveGo.transform, false);
            handleGo.transform.localPosition = new Vector3(0f, 0f, 0.05f);
            handleGo.transform.localRotation = Quaternion.identity;

            // 5-1. 휠 림(납작한 원반)
            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Undo.RegisterCreatedObjectUndo(rim, "Create Wheel Rim");
            rim.name = "WheelRim";
            rim.transform.SetParent(handleGo.transform, false);
            rim.transform.localPosition = Vector3.zero;
            rim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rim.transform.localScale = new Vector3(WheelRadius * 2f, 0.015f, WheelRadius * 2f);
            DestroyChildCollider(rim);

            // 5-2. 살(스포크) 4개, 45도 간격으로 배치
            for (int i = 0; i < 4; i++)
            {
                var spoke = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(spoke, $"Create Spoke {i}");
                spoke.name = $"Spoke_{i}";
                spoke.transform.SetParent(handleGo.transform, false);
                spoke.transform.localPosition = Vector3.zero;
                spoke.transform.localRotation = Quaternion.AngleAxis(i * 45f, Vector3.forward);
                spoke.transform.localScale = new Vector3(WheelRadius * 1.8f, 0.02f, 0.02f);
                DestroyChildCollider(spoke);
            }

            // 5-3. 핸들 그랩용 콜라이더 (휠 림 두께만큼의 박스 — 물리 충돌용)
            var handleCol = handleGo.AddComponent<BoxCollider>();
            handleCol.size = new Vector3(WheelRadius * 2.2f, WheelRadius * 2.2f, 0.06f);
            handleCol.isTrigger = false;

            // 5-4. 컨트롤러 진입 감지용 Trigger Sphere
            //      ControllerValveGrabber가 [RequireComponent(typeof(Collider))]를 요구하고
            //      Trigger 콜라이더로 anchor 진입을 감지한다.
            var grabZone = handleGo.AddComponent<SphereCollider>();
            grabZone.radius = WheelRadius * 1.3f;
            grabZone.isTrigger = true;

            // === 6. 컴포넌트 와이어링 ==========================================
            // Valve 루트: NetworkObject + Rigidbody(kinematic) + RadiatorValve
            if (valveGo.GetComponent<NetworkObject>() == null)
                Undo.AddComponent<NetworkObject>(valveGo);

            var rb = valveGo.GetComponent<Rigidbody>();
            if (rb == null) rb = Undo.AddComponent<Rigidbody>(valveGo);
            rb.useGravity = false;
            rb.isKinematic = true;

            var valve = Undo.AddComponent<RadiatorValve>(valveGo);
            using (var so = new SerializedObject(valve))
            {
                so.FindProperty("handleTransform").objectReferenceValue = handleGo.transform;
                so.FindProperty("rotationAxisLocal").vector3Value = new Vector3(0f, 0f, 1f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // ValveHandle: 그랩 인터랙션 측정기
            var grab = Undo.AddComponent<ValveRotationGrab>(handleGo);
            using (var sg = new SerializedObject(grab))
            {
                sg.FindProperty("valve").objectReferenceValue = valve;
                sg.FindProperty("pivot").objectReferenceValue = handleGo.transform;
                sg.FindProperty("rotationAxisLocal").vector3Value = new Vector3(0f, 0f, 1f);
                sg.ApplyModifiedPropertiesWithoutUndo();
            }

            // ValveHandle: 컨트롤러 입력 → BeginGrab/EndGrab 디스패처
            var ctrlGrab = Undo.AddComponent<ControllerValveGrabber>(handleGo);
            using (var sc = new SerializedObject(ctrlGrab))
            {
                sc.FindProperty("valveGrab").objectReferenceValue = grab;
                sc.ApplyModifiedPropertiesWithoutUndo();
            }

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = valveGo;
            EditorGUIUtility.PingObject(valveGo);
            EditorSceneSave();

            Debug.Log("[Capstone] 라디에이터 배관/밸브 생성 완료.\n" +
                      "  • 컨트롤러 사용: 손을 휠 가까이 가져가 Grip(또는 Trigger)을 누른 채 손목을 돌리세요.\n" +
                      "  • Meta Interaction SDK를 쓰는 경우: ValveHandle에 HandGrabInteractable 추가 후 " +
                      "InteractableUnityEventWrapper의 WhenSelect → ValveRotationGrab.BeginGrab(Transform), " +
                      "WhenUnselect → ValveRotationGrab.EndGrab 로 연결하면 손추적도 함께 동작합니다.");
        }

        // ---------- 헬퍼 -----------------------------------------------------
        static Bounds EstimateLocalBounds(GameObject go)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) return mf.sharedMesh.bounds;
            var mc = go.GetComponent<MeshCollider>();
            if (mc != null && mc.sharedMesh != null) return mc.sharedMesh.bounds;
            return new Bounds(Vector3.zero, Vector3.one);
        }

        static GameObject CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
        }

        static GameObject CreatePipe(Transform parent, string name, Vector3 position,
                                     Vector3 rotationEuler, float radius, float length)
        {
            var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Undo.RegisterCreatedObjectUndo(pipe, $"Create {name}");
            pipe.name = name;
            pipe.transform.SetParent(parent, false);
            pipe.transform.localPosition = position;
            pipe.transform.localRotation = Quaternion.Euler(rotationEuler);
            // 기본 Cylinder는 높이 2, 반지름 0.5 → 길이 length(m)에 맞추려면 y스케일 = length/2
            pipe.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
            return pipe;
        }

        static void DestroyChildCollider(GameObject primitive)
        {
            var col = primitive.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        static void EditorSceneSave()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
#endif
