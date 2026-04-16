using System.Collections;
using System.IO;
using UnityEngine;
using Meta.XR.MRUtilityKit;

public class RoomScanPipeline : MonoBehaviour
{
    private MRUK _mruk;

    public void Initialize(MRUK mrukInstance)
    {
        _mruk = mrukInstance;
    }

    /// <summary>
    /// Executes the room scan pipeline. Triggers space setup and loads the MRUK room.
    /// </summary>
    public IEnumerator RunScan(System.Action<string> onStatusUpdate, System.Action<MRUKRoom> onSuccess, System.Action onFailed)
    {
        if (_mruk == null)
        {
            onStatusUpdate?.Invoke("❌ MRUK instance not found");
            onFailed?.Invoke();
            yield break;
        }

        onStatusUpdate?.Invoke("방 스캔을 시작합니다.\n시스템 UI를 따라 방을 스캔해주세요.");

#if UNITY_ANDROID && !UNITY_EDITOR
        var setupTask = OVRScene.RequestSpaceSetup();
        yield return new WaitUntil(() => setupTask.IsCompleted);

        if (!setupTask.GetResult())
        {
            onStatusUpdate?.Invoke("❌ 방 스캔이 취소되었습니다.");
            onFailed?.Invoke();
            yield break;
        }
#endif

        onStatusUpdate?.Invoke("방 데이터를 불러오는 중...");
        var loadTask = _mruk.LoadSceneFromDevice(requestSceneCaptureIfNoDataFound: false);
        yield return new WaitUntil(() => loadTask.IsCompleted);

        if (loadTask.IsFaulted)
        {
            onStatusUpdate?.Invoke($"❌ 로드 실패: {loadTask.Exception?.GetBaseException().Message}");
            onFailed?.Invoke();
            yield break;
        }

        var room = _mruk.GetCurrentRoom();
        if (room == null)
        {
            onStatusUpdate?.Invoke("❌ 방 데이터를 찾을 수 없습니다.\nQuest Settings > Physical Space > Space Setup");
            onFailed?.Invoke();
            yield break;
        }

        onStatusUpdate?.Invoke("✅ 방 데이터 로드 완료!");
        SaveRoomJson(onStatusUpdate);
        
        yield return new WaitForSeconds(0.5f);
        onSuccess?.Invoke(room);
    }

    private void SaveRoomJson(System.Action<string> onStatusUpdate)
    {
        if (_mruk == null) return;

        string json = _mruk.SaveSceneToJsonString(false, null);
        string path = "/sdcard/Oculus/VideoShots/room.json";
        try
        {
            File.WriteAllText(path, json);
            onStatusUpdate?.Invoke($"✅ Scan complete! (saved)");
        }
        catch (System.Exception)
        {
            onStatusUpdate?.Invoke("⚠️ Save failed");
        }
    }
}
