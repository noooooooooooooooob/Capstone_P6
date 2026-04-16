using UnityEngine;
using Meta.XR.MRUtilityKit;
using System.Collections;
using Puzzle;

namespace Puzzle
{
    public class EnvironmentScanner : MonoBehaviour
    {
        [Header("Override (테스트용)")]
        [SerializeField] private bool overrideEnvironment = false;
        [SerializeField] private EnvironmentType overrideType = EnvironmentType.Desk;

        [Header("Settings")]
        [SerializeField] private float mrukTimeoutSeconds = 5f;

        public EnvironmentType DetectedType { get; private set; } = EnvironmentType.Unknown;
        public bool IsReady { get; private set; } = false;

        private MRUK mruk;

        private void Start()
        {
            if (overrideEnvironment)
            {
                DetectedType = overrideType;
                IsReady = true;
                Debug.Log($"[EnvironmentScanner] Override: {DetectedType}");
                return;
            }

            mruk = FindFirstObjectByType<MRUK>();
            if (mruk != null)
            {
                mruk.RoomCreatedEvent.AddListener(OnRoomCreated);
                // 이미 방이 로드된 경우 대비
                StartCoroutine(WaitForRoomOrTimeout());
            }
            else
            {
                Debug.LogWarning("[EnvironmentScanner] MRUK not found. Defaulting to EmptyRoom.");
                DetectedType = EnvironmentType.EmptyRoom;
                IsReady = true;
            }
        }

        private IEnumerator WaitForRoomOrTimeout()
        {
            float elapsed = 0f;
            while (!IsReady && elapsed < mrukTimeoutSeconds)
            {
                // 이미 방 있으면 바로 처리
                var room = FindFirstObjectByType<MRUKRoom>();
                if (room != null)
                {
                    OnRoomCreated(room);
                    yield break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!IsReady)
            {
                Debug.LogWarning($"[EnvironmentScanner] MRUK timeout {mrukTimeoutSeconds}s. Defaulting to EmptyRoom.");
                DetectedType = EnvironmentType.EmptyRoom;
                IsReady = true;
            }
        }

        private void OnRoomCreated(MRUKRoom room)
        {
            if (IsReady) return;
            DetectedType = EvaluateRoom(room);
            IsReady = true;
            Debug.Log($"[EnvironmentScanner] Detected: {DetectedType}");
        }

        private EnvironmentType EvaluateRoom(MRUKRoom room)
        {
            bool hasDesk = false;
            bool hasChair = false;

            foreach (var anchor in room.Anchors)
            {
                Debug.Log($"[EnvironmentScanner] Anchor label: {anchor.Label} pos: {anchor.transform.position}");

                if (anchor.Label == MRUKAnchor.SceneLabels.TABLE ||
                    anchor.Label == MRUKAnchor.SceneLabels.COUCH ||
                    anchor.Label == MRUKAnchor.SceneLabels.BED)
                    hasDesk = true;

                if (anchor.Label == MRUKAnchor.SceneLabels.OTHER)
                    hasChair = true;
            }

            if (hasDesk) return EnvironmentType.Desk;
            if (hasChair) return EnvironmentType.Chair;
            return EnvironmentType.EmptyRoom;
        }
    }
}
