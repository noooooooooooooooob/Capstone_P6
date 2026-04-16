
#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using Puzzle;

/// <summary>
/// 에디터 메뉴에서 퍼즐 씬 세팅을 자동으로 완성합니다.
/// Menu: Puzzle > Setup Scene
/// </summary>
public class PuzzleSceneSetup : MonoBehaviour
{
    [MenuItem("Puzzle/Setup Scene")]
    public static void SetupScene()
    {
        // ── 1. PuzzleRoot 찾기 ──────────────────────────
        var root = GameObject.Find("PuzzleRoot");
        if (root == null) { Debug.LogError("[Setup] PuzzleRoot not found."); return; }

        var lockUI = GameObject.Find("LockUI");
        if (lockUI == null) { Debug.LogError("[Setup] LockUI not found."); return; }

        // ── 2. 버튼 그리드 생성 ─────────────────────────
        var buttonGrid = new GameObject("ButtonGrid");
        buttonGrid.transform.SetParent(lockUI.transform, false);
        var gridRt = buttonGrid.AddComponent<RectTransform>();
        gridRt.anchoredPosition = new Vector2(0, -60);

        var btnDefs = new (string label, float x, float y)[]
        {
            ("1",-80,60), ("2",0,60),   ("3",80,60),
            ("4",-80,0),  ("5",0,0),    ("6",80,0),
            ("7",-80,-60),("8",0,-60),  ("9",80,-60),
            ("DEL",-80,-120),("0",0,-120),("OK",80,-120),
        };

        var numberButtons = new Button[10];
        Button deleteButton = null, confirmButton = null;

        foreach (var (label, x, y) in btnDefs)
        {
            var go = new GameObject($"Btn_{label}");
            go.transform.SetParent(buttonGrid.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(70, 50);

            var img = go.AddComponent<Image>();
            img.color = label == "OK"  ? new Color(0.2f, 0.7f, 0.2f)
                      : label == "DEL" ? new Color(0.7f, 0.2f, 0.2f)
                      : new Color(0.25f, 0.25f, 0.25f);

            var btn = go.AddComponent<Button>();

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = textRt.offsetMax = Vector2.zero;
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 24;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            if (label == "DEL") deleteButton = btn;
            else if (label == "OK") confirmButton = btn;
            else if (int.TryParse(label, out int num)) numberButtons[num] = btn;
        }

        // ── 3. InputDisplay / FeedbackText 텍스트 생성 ──
        var inputDisplayGo = new GameObject("InputDisplay");
        inputDisplayGo.transform.SetParent(lockUI.transform, false);
        var inputRt = inputDisplayGo.AddComponent<RectTransform>();
        inputRt.anchoredPosition = new Vector2(0, 80);
        inputRt.sizeDelta = new Vector2(300, 60);
        var inputTmp = inputDisplayGo.AddComponent<TextMeshProUGUI>();
        inputTmp.text = "_ _ _ _";
        inputTmp.fontSize = 48;
        inputTmp.alignment = TextAlignmentOptions.Center;
        inputTmp.color = Color.white;

        var feedbackGo = new GameObject("FeedbackText");
        feedbackGo.transform.SetParent(lockUI.transform, false);
        var feedRt = feedbackGo.AddComponent<RectTransform>();
        feedRt.anchoredPosition = new Vector2(0, 20);
        feedRt.sizeDelta = new Vector2(300, 40);
        var feedTmp = feedbackGo.AddComponent<TextMeshProUGUI>();
        feedTmp.text = "";
        feedTmp.fontSize = 28;
        feedTmp.alignment = TextAlignmentOptions.Center;

        // ── 4. LockController에 레퍼런스 연결 ───────────
        var lockCtrl = lockUI.GetComponent<LockController>();
        if (lockCtrl != null)
        {
            var so = new SerializedObject(lockCtrl);
            so.FindProperty("inputDisplay").objectReferenceValue = inputTmp;
            so.FindProperty("feedbackText").objectReferenceValue = feedTmp;

            var btnArray = so.FindProperty("numberButtons");
            btnArray.arraySize = 10;
            for (int i = 0; i < 10; i++)
                btnArray.GetArrayElementAtIndex(i).objectReferenceValue = numberButtons[i];

            so.FindProperty("deleteButton").objectReferenceValue = deleteButton;
            so.FindProperty("confirmButton").objectReferenceValue = confirmButton;
            so.ApplyModifiedProperties();
        }

        // ── 5. PuzzleManager에 레퍼런스 연결 ────────────
        var pm = root.GetComponent<PuzzleManager>();
        var es = root.GetComponent<EnvironmentScanner>();
        if (pm != null)
        {
            var so = new SerializedObject(pm);
            so.FindProperty("environmentScanner").objectReferenceValue = es;
            so.FindProperty("chairPuzzle").objectReferenceValue = root.transform.Find("ChairPuzzle")?.GetComponent<ChairPuzzle>();
            so.FindProperty("deskPuzzle").objectReferenceValue = root.transform.Find("DeskPuzzle")?.GetComponent<DeskPuzzle>();
            so.FindProperty("emptyRoomPuzzle").objectReferenceValue = root.transform.Find("EmptyRoomPuzzle")?.GetComponent<EmptyRoomPuzzle>();
            so.FindProperty("lockController").objectReferenceValue = lockCtrl;
            so.ApplyModifiedProperties();
        }

        // ── 6. EmptyRoomPuzzle에 프리팹 연결 ────────────
        var erp = root.transform.Find("EmptyRoomPuzzle")?.GetComponent<EmptyRoomPuzzle>();
        if (erp != null)
        {
            var cubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/VirtualColorCube.prefab");
            var so = new SerializedObject(erp);
            so.FindProperty("virtualObjectPrefab").objectReferenceValue = cubePrefab;
            so.FindProperty("virtualLockPrefab").objectReferenceValue = cubePrefab; // 임시 동일 프리팹
            so.ApplyModifiedProperties();
        }

        // ── 7. ChairPuzzle에 기존 Cube 연결 ─────────────
        var chairPuzzle = root.transform.Find("ChairPuzzle")?.GetComponent<ChairPuzzle>();
        var existingCube = GameObject.Find("[BuildingBlock] Cube");
        if (chairPuzzle != null && existingCube != null)
        {
            var mr = existingCube.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var so = new SerializedObject(chairPuzzle);
                var arr = so.FindProperty("colorTargetObjects");
                arr.arraySize = 1;
                arr.GetArrayElementAtIndex(0).objectReferenceValue = mr;
                so.ApplyModifiedProperties();
            }
        }

        // ── 8. 씬 저장 ───────────────────────────────────
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[PuzzleSceneSetup] ✅ Scene setup complete! Save the scene (Ctrl+S).");
    }
}
#endif
