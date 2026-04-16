#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

public class FixLockUIScale : MonoBehaviour
{
    [MenuItem("Puzzle/Fix LockUI Scale")]
    public static void Fix()
    {
        var lockUI = GameObject.Find("LockUI");
        if (lockUI == null) { Debug.LogError("LockUI not found"); return; }

        // World Space Canvas 스케일 - 1unit = 1000px 기준
        lockUI.transform.localScale = Vector3.one * 0.001f;
        lockUI.transform.position = new Vector3(0f, 1.4f, 0.8f);

        // RectTransform 크기 400x500 (실제 월드 크기 0.4m x 0.5m)
        var rt = lockUI.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(400, 500);

        // CanvasScaler
        var scaler = lockUI.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = lockUI.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10;

        // GraphicRaycaster (XR 인터랙션에 필요)
        if (lockUI.GetComponent<GraphicRaycaster>() == null)
            lockUI.AddComponent<GraphicRaycaster>();

        // ButtonGrid RectTransform 위치 재조정
        var buttonGrid = GameObject.Find("ButtonGrid");
        if (buttonGrid != null)
        {
            var brt = buttonGrid.GetComponent<RectTransform>();
            if (brt != null) brt.anchoredPosition = new Vector2(0, -80);
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[FixLockUIScale] ✅ Done. Canvas: 0.4m x 0.5m in world space.");
    }
}
#endif
