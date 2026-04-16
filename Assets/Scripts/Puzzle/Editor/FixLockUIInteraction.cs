#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using Puzzle;
using UnityEditor;

public class FixLockUIInteraction : MonoBehaviour
{
    [MenuItem("Puzzle/Fix LockUI Interaction")]
    public static void Fix()
    {
        var lockUI = GameObject.Find("LockUI");
        if (lockUI == null) { Debug.LogError("LockUI not found"); return; }

        // GraphicRaycaster 확인 (기본 UI 레이캐스트)
        if (lockUI.GetComponent<GraphicRaycaster>() == null)
            lockUI.AddComponent<GraphicRaycaster>();

        // OVRRaycaster 추가 (Quest 컨트롤러 포인터 지원)
        // OVRRaycaster가 없으면 일반 GraphicRaycaster만 사용
        var ovrType = System.Type.GetType("OVRRaycaster, Assembly-CSharp");
        if (ovrType == null)
            ovrType = System.Type.GetType("OVRRaycaster, Oculus.VR");

        if (ovrType != null)
        {
            if (lockUI.GetComponent(ovrType) == null)
            {
                lockUI.AddComponent(ovrType);
                Debug.Log("[FixLockUIInteraction] OVRRaycaster added.");
            }
        }
        else
        {
            Debug.Log("[FixLockUIInteraction] OVRRaycaster not found in project — using GraphicRaycaster only.");
        }

        // Canvas를 World Space로 재확인
        var canvas = lockUI.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
        }

        // LockUI를 항상 플레이어를 향하도록 Billboard 컴포넌트 추가
        if (lockUI.GetComponent<LockUIBillboard>() == null)
            lockUI.AddComponent<LockUIBillboard>();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[FixLockUIInteraction] ✅ Done.");
    }
}
#endif
