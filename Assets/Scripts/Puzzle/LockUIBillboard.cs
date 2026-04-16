using UnityEngine;

namespace Puzzle
{
    /// <summary>
    /// LockUI 캔버스가 항상 플레이어 카메라를 향하도록 합니다.
    /// </summary>
    public class LockUIBillboard : MonoBehaviour
    {
        private Transform camTransform;

private void Start()
        {
            camTransform = Camera.main?.transform;

            // World Space Canvas에 worldCamera 연결 (Ray 클릭에 필수)
            var canvas = GetComponent<Canvas>();
            if (canvas != null && Camera.main != null)
            {
                canvas.worldCamera = Camera.main;
                Debug.Log("[LockUIBillboard] Canvas.worldCamera set to Camera.main.");
            }
        }

        private void LateUpdate()
        {
            if (camTransform == null)
            {
                camTransform = Camera.main?.transform;
                return;
            }

            // 카메라 방향을 바라보되 Y축만 회전 (수직은 고정)
            Vector3 direction = transform.position - camTransform.position;
            direction.y = 0;

            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}
