using System.Collections.Generic;
using UnityEngine;

public class DummyRoomBuilder : MonoBehaviour
{
    [Header("Dummy Room Dimensions")]
    [SerializeField] private float roomWidth  = 4f;
    [SerializeField] private float roomDepth  = 5f;
    [SerializeField] private float roomHeight = 2.5f;

    [Header("Dummy Room Materials")]
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Material floorMaterial;
    [SerializeField] private Material ceilingMaterial;

    private readonly List<GameObject> _spawnedSurfaces = new();

    public void BuildDummyRoom()
    {
        ClearDummyRoom();

        float w = roomWidth, d = roomDepth, h = roomHeight;
        float hw = w * 0.5f, hd = d * 0.5f, hh = h * 0.5f;

        Material wallMat    = wallMaterial    ?? CreateColorMaterial(new Color(0.8f, 0.8f, 0.8f));
        Material floorMat   = floorMaterial   ?? CreateColorMaterial(new Color(0.4f, 0.4f, 0.4f));
        Material ceilingMat = ceilingMaterial ?? CreateColorMaterial(new Color(0.9f, 0.9f, 0.9f));

        SpawnDummyQuad("Floor",   new Vector3(0, 0, 0),    Quaternion.Euler(90, 0, 0),   w, d, floorMat);
        SpawnDummyQuad("Ceiling", new Vector3(0, h, 0),    Quaternion.Euler(-90, 0, 0),  w, d, ceilingMat);
        SpawnDummyQuad("Wall_F",  new Vector3(0, hh,  hd), Quaternion.Euler(0, 180, 0),  w, h, wallMat);
        SpawnDummyQuad("Wall_B",  new Vector3(0, hh, -hd), Quaternion.identity,          w, h, wallMat);
        SpawnDummyQuad("Wall_L",  new Vector3(-hw, hh, 0), Quaternion.Euler(0,  90, 0),  d, h, wallMat);
        SpawnDummyQuad("Wall_R",  new Vector3( hw, hh, 0), Quaternion.Euler(0, -90, 0),  d, h, wallMat);
    }

    public void ClearDummyRoom()
    {
        foreach (var go in _spawnedSurfaces)
        {
            if (go != null) Destroy(go);
        }
        _spawnedSurfaces.Clear();
    }

    private void SpawnDummyQuad(string label, Vector3 pos, Quaternion rot,
                                float width, float height, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = $"DummyRoom_{label}";
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale    = new Vector3(width, height, 1f);
        Destroy(go.GetComponent<MeshCollider>());
        
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = mat;
        
        _spawnedSurfaces.Add(go);
    }

    private Material CreateColorMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", color); // URP
        mat.color = color; // Standard
        return mat;
    }
}
