using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;
using TMPro;

public class RoomScanTest : MonoBehaviour
{
    [Header("MRUK")]
    [SerializeField] private MRUK mruk;

    [Header("Virtual Objects")]
    [SerializeField] private GameObject puzzlePrefab;
    [SerializeField] private GameObject portalPrefab;
    [SerializeField] private GameObject terminalPrefab;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject startButton;

    private MRUKRoom _room;
    private readonly List<GameObject> _spawned = new();

    public void StartTest()
    {
        startButton.SetActive(false);
        StartCoroutine(RunTest());
    }

    private IEnumerator RunTest()
    {
        SetStatus("방 스캔 중...");
        yield return mruk.LoadSceneFromDevice();

        _room = mruk.GetCurrentRoom();
        if (_room == null)
        {
            SetStatus("❌ 스캔 실패\nQuest 설정 > 물리적 공간 > 공간 설정 확인");
            startButton.SetActive(true);
            yield break;
        }

        SetStatus("✅ 스캔 완료!");
        yield return new WaitForSeconds(0.5f);

        SetStatus("가상 오브젝트 배치 중...");
        yield return PlaceObjects();

        SetStatus($"✅ 완료! 오브젝트 {_spawned.Count}개 배치됨");
    }

    private IEnumerator PlaceObjects()
    {
        PlaceAtFloorCenter();
        yield return null;

        PlaceOnWalls();
        yield return null;

        PlaceTerminal();
    }

    private void PlaceAtFloorCenter()
    {
        var floor = _room.FloorAnchor;  // ✅ v85
        if (floor == null) return;

        var pos = floor.transform.position;
        pos.y += 0.02f;

        var go = SpawnObject(portalPrefab, pos, Quaternion.identity, "Portal", Color.cyan);
        go.transform.localScale = Vector3.one * 0.5f;
    }

    private void PlaceOnWalls()
    {
        int count = 0;
        foreach (var anchor in _room.Anchors)
        {
            if (!anchor.HasLabel("WALL_FACE")) continue;  // ✅ v85
            if (count >= 4) break;

            var pos = anchor.transform.position + anchor.transform.forward * 0.1f;
            pos.y = GetFloorY() + 1.2f;

            SpawnObject(puzzlePrefab, pos,
                Quaternion.LookRotation(-anchor.transform.forward),
                $"Puzzle_{count}", Color.yellow);
            count++;
        }
    }

    private void PlaceTerminal()
    {
        var cam = Camera.main.transform;
        var pos = cam.position + cam.forward * 1.5f;
        pos.y = GetFloorY() + 1.0f;

        SpawnObject(terminalPrefab, pos,
            Quaternion.LookRotation(cam.forward),
            "Terminal", Color.green);
    }

    private float GetFloorY()
    {
        var floor = _room.FloorAnchor;  // ✅ v85
        return floor != null ? floor.transform.position.y : 0f;
    }

    private GameObject SpawnObject(GameObject prefab, Vector3 pos, Quaternion rot,
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
        Debug.Log($"[RoomScanTest] {label} → {pos}");
        return go;
    }

    private void SetStatus(string msg)
    {
        Debug.Log($"[RoomScanTest] {msg}");
        if (statusText != null) statusText.text = msg;
    }

    public void ResetTest()
    {
        foreach (var go in _spawned) Destroy(go);
        _spawned.Clear();
        startButton.SetActive(true);
        SetStatus("대기 중...");
    }
}