using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;

public class RoomObjectPlacer : MonoBehaviour
{
    [Header("Virtual Objects")]
    [SerializeField] private GameObject puzzlePrefab;
    [SerializeField] private GameObject portalPrefab;
    [SerializeField] private GameObject terminalPrefab;

    private readonly List<GameObject> _spawnedObjects = new();

    public void PlaceGameObjects(MRUKRoom room)
    {
        ClearObjects();

        PlacePortalAtFloor(room);
        PlacePuzzlesOnWalls(room);
        PlaceTerminalAtCamera(room);
    }

    public void ClearObjects()
    {
        foreach (var go in _spawnedObjects)
        {
            if (go != null) Destroy(go);
        }
        _spawnedObjects.Clear();
    }

    private void PlacePortalAtFloor(MRUKRoom room)
    {
        var floor = room.FloorAnchor;
        if (floor == null) return;

        var pos = floor.transform.position;
        pos.y += 0.02f;

        var go = SpawnGameObject(portalPrefab, pos, Quaternion.identity, "Portal", Color.cyan);
        go.transform.localScale = Vector3.one * 0.5f;
    }

    private void PlacePuzzlesOnWalls(MRUKRoom room)
    {
        int count = 0;
        foreach (var anchor in room.Anchors)
        {
            if (!anchor.HasLabel("WALL_FACE")) continue;
            if (count >= 4) break;

            var pos = anchor.transform.position + anchor.transform.forward * 0.1f;
            pos.y = GetFloorY(room) + 1.2f;

            SpawnGameObject(puzzlePrefab, pos,
                Quaternion.LookRotation(-anchor.transform.forward),
                $"Puzzle_{count}", Color.yellow);
            count++;
        }
    }

    private void PlaceTerminalAtCamera(MRUKRoom room)
    {
        if (Camera.main == null) return;
        
        var cam = Camera.main.transform;
        var pos = cam.position + cam.forward * 1.5f;
        pos.y = GetFloorY(room) + 1.0f;

        SpawnGameObject(terminalPrefab, pos,
            Quaternion.LookRotation(cam.forward),
            "Terminal", Color.green);
    }

    private float GetFloorY(MRUKRoom room)
    {
        var floor = room.FloorAnchor;
        return floor != null ? floor.transform.position.y : 0f;
    }

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
        _spawnedObjects.Add(go);
        return go;
    }
}
