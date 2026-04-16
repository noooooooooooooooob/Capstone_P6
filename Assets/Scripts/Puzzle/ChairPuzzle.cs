using UnityEngine;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;

namespace Puzzle
{
    public class ChairPuzzle : MonoBehaviour
    {
        [SerializeField] private GameObject colorCubePrefab;
        [SerializeField] private float cubeSeparation = 0.12f;
        [SerializeField] private float heightAboveSurface = 0.08f;
        [SerializeField] private List<UnityEngine.UI.Image> colorIndicators;

        private List<GameObject> spawnedCubes = new List<GameObject>();

        public void Initialize(IReadOnlyList<PuzzleColor> colorSequence)
        {
            ClearCubes();
            SpawnColorCubes(colorSequence);
            UpdateColorIndicators(colorSequence);
        }

        private void SpawnColorCubes(IReadOnlyList<PuzzleColor> sequence)
        {
            if (colorCubePrefab == null) { Debug.LogWarning("[ChairPuzzle] colorCubePrefab not assigned."); return; }

            Vector3 origin = FindAnchorSurfacePosition();
            float totalWidth = (sequence.Count - 1) * cubeSeparation;

            for (int i = 0; i < sequence.Count; i++)
            {
                Vector3 pos = origin + Vector3.right * (i * cubeSeparation - totalWidth * 0.5f);
                var cube = Instantiate(colorCubePrefab, pos, Quaternion.identity, transform);
                cube.name = $"ChairCube_{sequence[i]}";
                PuzzleColorHelper.ApplyColor(cube.GetComponent<MeshRenderer>(), sequence[i]);
                spawnedCubes.Add(cube);
            }
        }

        // Other 라벨 앵커(의자/소품) 위에 소환
        private Vector3 FindAnchorSurfacePosition()
        {
            var room = FindFirstObjectByType<MRUKRoom>();
            if (room != null)
            {
                foreach (var anchor in room.Anchors)
                {
                    if (anchor.Label == MRUKAnchor.SceneLabels.OTHER ||
                        anchor.Label == MRUKAnchor.SceneLabels.COUCH)
                    {
                        Vector3 pos = anchor.transform.position;
                        float halfH = Mathf.Abs(anchor.transform.localScale.y) * 0.5f;
                        Debug.Log($"[ChairPuzzle] Found anchor: {anchor.Label} at {pos}");
                        return new Vector3(pos.x, pos.y + halfH + heightAboveSurface, pos.z);
                    }
                }
                // 못 찾으면 첫 번째 앵커 사용
                if (room.Anchors.Count > 0)
                {
                    var first = room.Anchors[0];
                    Debug.Log($"[ChairPuzzle] Fallback to first anchor: {first.Label}");
                    return first.transform.position + Vector3.up * heightAboveSurface;
                }
            }
            return GetCameraFrontPosition(0.5f);
        }

        private Vector3 GetCameraFrontPosition(float height)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 fwd = new Vector3(cam.transform.forward.x, 0, cam.transform.forward.z).normalized;
                return cam.transform.position + fwd * 1.0f + new Vector3(0, height - cam.transform.position.y, 0);
            }
            return new Vector3(0, height, 1f);
        }

        private void UpdateColorIndicators(IReadOnlyList<PuzzleColor> sequence)
        {
            for (int i = 0; i < colorIndicators.Count; i++)
            {
                if (colorIndicators[i] == null) continue;
                colorIndicators[i].color = i < sequence.Count
                    ? PuzzleColorHelper.GetColor(sequence[i])
                    : Color.white;
            }
        }

        private void ClearCubes()
        {
            foreach (var c in spawnedCubes) if (c != null) Destroy(c);
            spawnedCubes.Clear();
        }
    }
}
