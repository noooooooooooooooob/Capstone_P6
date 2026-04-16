using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;

public class RoomVisualizer : MonoBehaviour
{
    [Header("Surface Materials")]
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Material floorMaterial;
    [SerializeField] private Material ceilingMaterial;
    [SerializeField] private Material furnitureMaterial;
    [SerializeField] private Material fallbackMaterial;

    private readonly List<GameObject> _spawnedSurfaces = new();

    private static readonly Dictionary<string, Color> LabelColors = new()
    {
        { "WALL_FACE", new Color(1.0f, 1.0f, 1.0f) },
        { "FLOOR",     new Color(1.0f, 1.0f, 1.0f) },
        { "CEILING",   new Color(1.0f, 1.0f, 1.0f) },
    };

    public void BuildRoomVisuals(MRUKRoom room)
    {
        ClearVisuals();

        foreach (var anchor in room.Anchors)
        {
            if (!anchor.PlaneRect.HasValue) continue;

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

            go.transform.position = anchor.transform.position;
            go.transform.rotation = anchor.transform.rotation * Quaternion.Euler(0, 180, 0);
            go.transform.localScale = new Vector3(rect.width, rect.height, 1f);

            _spawnedSurfaces.Add(go);
        }
    }

    public void ClearVisuals()
    {
        foreach (var go in _spawnedSurfaces)
        {
            if (go != null) Destroy(go);
        }
        _spawnedSurfaces.Clear();
    }

    private Material ResolveMaterial(MRUKAnchor anchor)
    {
        if (anchor.HasLabel("FLOOR"))     return floorMaterial    ?? CreateColorMaterial("FLOOR");
        if (anchor.HasLabel("CEILING"))   return ceilingMaterial  ?? CreateColorMaterial("CEILING");
        if (anchor.HasLabel("WALL_FACE")) return wallMaterial     ?? CreateColorMaterial("WALL_FACE");
        return furnitureMaterial ?? CreateColorMaterial("OTHER");
    }

    private Material CreateColorMaterial(string label)
    {
        Material baseMat = fallbackMaterial ?? wallMaterial ?? floorMaterial ?? ceilingMaterial;
        if (baseMat == null)
        {
            return new Material(Shader.Find("Hidden/Universal Render Pipeline/FallbackError"));
        }
        var mat = new Material(baseMat);
        Color color = LabelColors.TryGetValue(label, out var c) ? c : new Color(1f, 0.5f, 0f);
        mat.SetColor("_BaseColor", color);
        return mat;
    }
}
