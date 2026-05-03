using UnityEngine;

namespace Capstone.Puzzle
{
    /// <summary>
    /// Quest 컨트롤러의 Grip(또는 IndexTrigger) 입력으로 밸브 핸들을 잡고
    /// 손목을 돌려 회전시키는 폴백 그랩 핸들러.
    ///
    /// 동작 흐름:
    ///   1. 핸들에 부착된 Trigger Collider 영역 안으로 컨트롤러 anchor가 들어옴
    ///   2. 사용자가 Grip 버튼을 누름 → <see cref="ValveRotationGrab.BeginGrab(Transform)"/> 호출
    ///   3. 손을 돌리면 <see cref="ValveRotationGrab"/>이 회전 델타를 측정해
    ///      <see cref="RadiatorValve.ApplyRotationDelta(float)"/>를 호출 (Photon Fusion 동기화)
    ///   4. Grip을 떼면 <see cref="ValveRotationGrab.EndGrab"/> 호출
    ///
    /// Meta Interaction SDK의 HandGrabInteractable이 아직 셋업되지 않아도
    /// 컨트롤러만 있으면 즉시 동작하도록 OVRInput만 사용한다.
    /// (Meta XR Core SDK 85.0.0에 OVRInput / OVRCameraRig가 포함되어 있음)
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class ControllerValveGrabber : MonoBehaviour
    {
        public enum GrabButton
        {
            Grip,            // 손가락 모양으로 쥐는 그립 버튼 (PrimaryHandTrigger)
            IndexTrigger,    // 검지 트리거 (PrimaryIndexTrigger)
            Either           // 둘 중 하나라도 눌리면 그랩
        }

        [Header("연결")]
        [Tooltip("회전 처리를 담당하는 ValveRotationGrab 컴포넌트")]
        [SerializeField] ValveRotationGrab valveGrab;

        [Tooltip("좌측 컨트롤러 anchor Transform. 비워두면 OVRCameraRig에서 자동 탐색.")]
        [SerializeField] Transform leftControllerAnchor;

        [Tooltip("우측 컨트롤러 anchor Transform. 비워두면 OVRCameraRig에서 자동 탐색.")]
        [SerializeField] Transform rightControllerAnchor;

        [Header("입력")]
        [SerializeField] GrabButton grabButton = GrabButton.Either;

        [Tooltip("아날로그 입력에서 그랩으로 인식할 임계값(0~1)")]
        [Range(0.05f, 1f)]
        [SerializeField] float grabThreshold = 0.6f;

        [Header("진입 감지")]
        [Tooltip("컨트롤러 anchor에 콜라이더가 없을 때, 거리 기반으로 영역 진입을 직접 판정한다.")]
        [SerializeField] bool useDistanceCheckFallback = true;

        // 현재 핸들 영역 안에 있는(또는 그랩 중인) 컨트롤러
        OVRInput.Controller _activeController = OVRInput.Controller.None;
        bool _isGrabbing;

        Collider _zoneCollider;

        void Reset()
        {
            // 빌더 스크립트가 부착할 때 적절한 기본값을 잡아준다.
            _zoneCollider = GetComponent<Collider>();
            if (_zoneCollider != null) _zoneCollider.isTrigger = true;
            valveGrab = GetComponent<ValveRotationGrab>();
        }

        void Awake()
        {
            _zoneCollider = GetComponent<Collider>();
            if (valveGrab == null) valveGrab = GetComponent<ValveRotationGrab>();
            AutoFindAnchors();
        }

        void OnTriggerEnter(Collider other)
        {
            // 컨트롤러 anchor에 직접 콜라이더가 부착되어 있다면 트리거 이벤트로 감지.
            var ctrl = ResolveController(other.transform);
            if (ctrl != OVRInput.Controller.None) _activeController = ctrl;
        }

        void OnTriggerExit(Collider other)
        {
            var ctrl = ResolveController(other.transform);
            if (ctrl == _activeController && !_isGrabbing)
                _activeController = OVRInput.Controller.None;
        }

        void Update()
        {
            // 거리 기반 폴백: anchor에 콜라이더가 없을 때도 동작하도록.
            if (useDistanceCheckFallback && _activeController == OVRInput.Controller.None && !_isGrabbing)
            {
                _activeController = ResolveControllerByDistance();
            }

            if (_activeController == OVRInput.Controller.None) return;
            if (valveGrab == null) return;

            bool pressed = ReadGrabPressed(_activeController);

            if (pressed && !_isGrabbing)
            {
                var anchor = GetAnchor(_activeController);
                if (anchor == null) return;
                _isGrabbing = true;
                valveGrab.BeginGrab(anchor);
            }
            else if (!pressed && _isGrabbing)
            {
                _isGrabbing = false;
                valveGrab.EndGrab();

                // 손을 떼고 영역 밖이면 active controller 해제
                if (!IsAnchorInsideZone(_activeController))
                    _activeController = OVRInput.Controller.None;
            }
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        bool ReadGrabPressed(OVRInput.Controller ctrl)
        {
            float grip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, ctrl);
            float trig = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, ctrl);
            switch (grabButton)
            {
                case GrabButton.Grip:         return grip >= grabThreshold;
                case GrabButton.IndexTrigger: return trig >= grabThreshold;
                case GrabButton.Either:
                default:                      return grip >= grabThreshold || trig >= grabThreshold;
            }
        }

        OVRInput.Controller ResolveController(Transform t)
        {
            if (leftControllerAnchor != null && IsSelfOrChildOf(t, leftControllerAnchor))
                return OVRInput.Controller.LTouch;
            if (rightControllerAnchor != null && IsSelfOrChildOf(t, rightControllerAnchor))
                return OVRInput.Controller.RTouch;
            return OVRInput.Controller.None;
        }

        static bool IsSelfOrChildOf(Transform candidate, Transform parent)
        {
            for (var t = candidate; t != null; t = t.parent)
                if (t == parent) return true;
            return false;
        }

        Transform GetAnchor(OVRInput.Controller ctrl)
        {
            return ctrl == OVRInput.Controller.LTouch ? leftControllerAnchor : rightControllerAnchor;
        }

        bool IsAnchorInsideZone(OVRInput.Controller ctrl)
        {
            if (_zoneCollider == null) return false;
            var anchor = GetAnchor(ctrl);
            if (anchor == null) return false;
            return _zoneCollider.bounds.Contains(anchor.position);
        }

        OVRInput.Controller ResolveControllerByDistance()
        {
            if (_zoneCollider == null) return OVRInput.Controller.None;
            if (leftControllerAnchor != null && _zoneCollider.bounds.Contains(leftControllerAnchor.position))
                return OVRInput.Controller.LTouch;
            if (rightControllerAnchor != null && _zoneCollider.bounds.Contains(rightControllerAnchor.position))
                return OVRInput.Controller.RTouch;
            return OVRInput.Controller.None;
        }

        void AutoFindAnchors()
        {
            if (leftControllerAnchor != null && rightControllerAnchor != null) return;

            var rig = FindObjectOfType<OVRCameraRig>();
            if (rig == null) return;

            if (leftControllerAnchor == null)  leftControllerAnchor  = rig.leftControllerAnchor;
            if (rightControllerAnchor == null) rightControllerAnchor = rig.rightControllerAnchor;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            var col = GetComponent<Collider>();
            if (col == null) return;
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.35f);
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
#endif
    }
}
