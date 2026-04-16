using UnityEngine;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;

namespace Puzzle
{
    public class DeskPuzzle : MonoBehaviour
    {
        [SerializeField] private GameObject colorCubePrefab;
        [SerializeField] private float cubeSeparation = 0.12f;
        [SerializeField] private float heightAboveSurface = 0.06f;
        [SerializeField] private Transform dropZoneParent;

        private List<GameObject> spawnedCubes = new List<GameObject>();

        public void Initialize(IReadOnlyList<PuzzleColor> colorSequence)
        {
            ClearCubes();
            SpawnColorCubes(colorSequence);
        }

        private void SpawnColorCubes(IReadOnlyList<PuzzleColor> sequence)
        {
            if (colorCubePrefab == null) { Debug.LogWarning("[DeskPuzzle] colorCubePrefab not assigned."); return; }

            Vector3 origin = FindDeskSurfacePosition();
            float totalWidth = (sequence.Count - 1) * cubeSeparation;

            for (int i = 0; i < sequence.Count; i++)
            {
                Vector3 pos = origin + Vector3.right * (i * cubeSeparation - totalWidth * 0.5f);
                var cube = Instantiate(colorCubePrefab, pos, Quaternion.identity, transform);
                cube.name = $"DeskCube_{sequence[i]}";
                PuzzleColorHelper.ApplyColor(cube.GetComponent<MeshRenderer>(), sequence[i]);
                var tag = cube.AddComponent<ColorTag>();
                tag.PuzzleColor = sequence[i];
                spawnedCubes.Add(cube);
            }

            if (dropZoneParent != null) dropZoneParent.gameObject.SetActive(true);
        }

        // TABLE/DESK/OTHER 순으로 앵커 탐색
        private Vector3 FindDeskSurfacePosition()
        {
            var room = FindFirstObjectByType<MRUKRoom>();
            if (room != null)
            {
                // 1순위: TABLE
                foreach (var anchor in room.Anchors)
                {
                    if (anchor.Label == MRUKAnchor.SceneLabels.TABLE)
                    {
                        Debug.Log($"[DeskPuzzle] Found TABLE at {anchor.transform.position}");
                        return SurfacePos(anchor);
                    }
                }
                // 2순위: OTHER (시뮬레이터에서 테이블이 OTHER로 잡히는 경우)
                foreach (var anchor in room.Anchors)
                {
                    if (anchor.Label == MRUKAnchor.SceneLabels.OTHER ||
                        anchor.Label == MRUKAnchor.SceneLabels.COUCH)
                    {
                        Debug.Log($"[DeskPuzzle] Fallback to {anchor.Label} at {anchor.transform.position}");
                        return SurfacePos(anchor);
                    }
                }
            }
            // 폴백: 카메라 앞 테이블 높이
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 fwd = new Vector3(cam.transform.forward.x, 0, cam.transform.forward.z).normalized;
                return cam.transform.position + fwd * 1.0f + new Vector3(0, 0.75f - cam.transform.position.y, 0);
            }
            return new Vector3(0, 0.75f, 1f);
        }

        private Vector3 SurfacePos(MRUKAnchor anchor)
        {
            Vector3 pos = anchor.transform.position;
            float halfH = Mathf.Abs(anchor.transform.localScale.y) * 0.5f;
            return new Vector3(pos.x, pos.y + halfH + heightAboveSurface, pos.z);
        }

        private void ClearCubes()
        {
            foreach (var c in spawnedCubes) if (c != null) Destroy(c);
            spawnedCubes.Clear();
        }
    }

    public class ColorTag : MonoBehaviour
    {
        public PuzzleColor PuzzleColor;
    }
}
