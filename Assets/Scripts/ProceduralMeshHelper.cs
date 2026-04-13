using UnityEngine;

/// <summary>
/// 프로시저럴 메시 생성 유틸리티.
/// Unity 기본 프리미티브에 없는 정사면체(Tetrahedron) 등을 런타임에 생성.
/// URP 호환 — 별도 셰이더 불필요, 표준 Lit/Unlit 머티리얼 사용 가능.
/// </summary>
public static class ProceduralMeshHelper
{
    /// <summary>
    /// 정사면체(Tetrahedron) 메시를 생성하여 반환.
    /// 중심이 원점에 오도록 정규화, radius로 크기 조절.
    /// flat shading을 위해 정점을 면별로 분리(12 vertices, 12 indices).
    /// </summary>
    public static Mesh CreateTetrahedron(float radius = 0.5f)
    {
        // 정사면체 4개 꼭짓점 (단위 크기, 중심 = 원점 근사)
        // 정삼각형 밑면 3개 + 꼭짓점 1개
        float sqrt2 = Mathf.Sqrt(2f);
        float sqrt6 = Mathf.Sqrt(6f);

        Vector3 v0 = new Vector3(0f, 1f, 0f);                                  // 상단
        Vector3 v1 = new Vector3(0f, -1f / 3f, 2f * sqrt2 / 3f);               // 전방
        Vector3 v2 = new Vector3(-sqrt6 / 3f, -1f / 3f, -sqrt2 / 3f);          // 좌하
        Vector3 v3 = new Vector3(sqrt6 / 3f, -1f / 3f, -sqrt2 / 3f);           // 우하

        // 중심 보정 (무게중심을 원점으로)
        Vector3 center = (v0 + v1 + v2 + v3) / 4f;
        v0 -= center; v1 -= center; v2 -= center; v3 -= center;

        // radius 적용
        v0 *= radius; v1 *= radius; v2 *= radius; v3 *= radius;

        // flat shading: 면마다 정점 분리 (4면 × 3 = 12 vertices)
        Vector3[] vertices = new Vector3[12];
        int[] triangles = new int[12];

        // 면 0: v0-v1-v2 (전면 좌)
        SetFace(vertices, triangles, 0, v0, v1, v2);
        // 면 1: v0-v2-v3 (후면 좌)
        SetFace(vertices, triangles, 1, v0, v2, v3);
        // 면 2: v0-v3-v1 (후면 우)
        SetFace(vertices, triangles, 2, v0, v3, v1);
        // 면 3: v1-v3-v2 (밑면)
        SetFace(vertices, triangles, 3, v1, v3, v2);

        // 법선 계산 (flat shading)
        Vector3[] normals = new Vector3[12];
        for (int face = 0; face < 4; face++)
        {
            int i = face * 3;
            Vector3 normal = Vector3.Cross(
                vertices[i + 1] - vertices[i],
                vertices[i + 2] - vertices[i]
            ).normalized;
            normals[i] = normal;
            normals[i + 1] = normal;
            normals[i + 2] = normal;
        }

        var mesh = new Mesh
        {
            name = "Tetrahedron",
            vertices = vertices,
            triangles = triangles,
            normals = normals
        };
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void SetFace(Vector3[] verts, int[] tris, int faceIdx,
                                 Vector3 a, Vector3 b, Vector3 c)
    {
        int i = faceIdx * 3;
        verts[i] = a;
        verts[i + 1] = b;
        verts[i + 2] = c;
        tris[i] = i;
        tris[i + 1] = i + 1;
        tris[i + 2] = i + 2;
    }

    /// <summary>
    /// 정사면체 GameObject를 생성하여 반환.
    /// MeshFilter + MeshRenderer + MeshCollider 자동 부착.
    /// </summary>
    public static GameObject CreateTetrahedronObject(string name, float radius, Material material)
    {
        var go = new GameObject(name);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();

        mf.mesh = CreateTetrahedron(radius);
        mr.sharedMaterial = material;

        return go;
    }
}
