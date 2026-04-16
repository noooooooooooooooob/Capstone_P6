using UnityEngine;
using System.Collections.Generic;

namespace Puzzle
{
    public class EmptyRoomPuzzle : MonoBehaviour
    {
        [SerializeField] private GameObject virtualObjectPrefab;
        [SerializeField] private GameObject virtualLockPrefab;
        [SerializeField] private float cubeSeparation = 0.12f;
        [SerializeField] private float spawnDistance = 1.2f;
        [SerializeField] private float spawnHeight = 1.0f;

        private List<GameObject> spawnedObjects = new List<GameObject>();
        private GameObject spawnedLock;

        public void Initialize(IReadOnlyList<PuzzleColor> colorSequence)
        {
            ClearAll();
            Vector3 basePos = GetSpawnPosition();
            SpawnVirtualObjects(colorSequence, basePos);
            SpawnVirtualLock(colorSequence, basePos);
        }

        private Vector3 GetSpawnPosition()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 fwd = new Vector3(cam.transform.forward.x, 0, cam.transform.forward.z).normalized;
                return new Vector3(
                    cam.transform.position.x + fwd.x * spawnDistance,
                    spawnHeight,
                    cam.transform.position.z + fwd.z * spawnDistance
                );
            }
            return new Vector3(0, spawnHeight, spawnDistance);
        }

        private void SpawnVirtualObjects(IReadOnlyList<PuzzleColor> sequence, Vector3 basePos)
        {
            if (virtualObjectPrefab == null) { Debug.LogWarning("[EmptyRoomPuzzle] virtualObjectPrefab not assigned."); return; }

            float totalWidth = (sequence.Count - 1) * cubeSeparation;
            for (int i = 0; i < sequence.Count; i++)
            {
                Vector3 pos = basePos + Vector3.right * (i * cubeSeparation - totalWidth * 0.5f);
                var obj = Instantiate(virtualObjectPrefab, pos, Quaternion.identity, transform);
                obj.name = $"VirtualObj_{sequence[i]}";
                PuzzleColorHelper.ApplyColor(obj.GetComponent<MeshRenderer>(), sequence[i]);
                var tag = obj.AddComponent<ColorTag>();
                tag.PuzzleColor = sequence[i];
                spawnedObjects.Add(obj);
            }
        }

        private void SpawnVirtualLock(IReadOnlyList<PuzzleColor> sequence, Vector3 basePos)
        {
            if (virtualLockPrefab == null) return;
            spawnedLock = Instantiate(virtualLockPrefab, basePos + Vector3.up * 0.3f, Quaternion.identity, transform);
            spawnedLock.name = "VirtualLock";
            spawnedLock.GetComponent<LockColorDisplay>()?.SetColorSequence(sequence);
        }

        private void ClearAll()
        {
            foreach (var o in spawnedObjects) if (o != null) Destroy(o);
            spawnedObjects.Clear();
            if (spawnedLock != null) Destroy(spawnedLock);
        }
    }

    public class LockColorDisplay : MonoBehaviour
    {
        [SerializeField] private List<MeshRenderer> colorSlots;

        public void SetColorSequence(IReadOnlyList<PuzzleColor> sequence)
        {
            for (int i = 0; i < colorSlots.Count && i < sequence.Count; i++)
                PuzzleColorHelper.ApplyColor(colorSlots[i], sequence[i]);
        }
    }
}
