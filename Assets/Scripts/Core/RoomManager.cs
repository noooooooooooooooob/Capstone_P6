using System.Collections;
using UnityEngine;
using Meta.XR.MRUtilityKit;
using TMPro;

/// <summary>
/// Coordinator for Room Scanning and Configuration.
/// Handles high-level logic, delegates specific behaviors to sub-components,
/// and ensures Quest 2 devices fall back to Dummy Room to prevent crashes.
/// </summary>
[RequireComponent(typeof(RoomScanPipeline))]
[RequireComponent(typeof(RoomVisualizer))]
[RequireComponent(typeof(RoomObjectPlacer))]
[RequireComponent(typeof(DummyRoomBuilder))]
public class RoomManager : MonoBehaviour
{
    public enum ScanPhase
    {
        Idle,            
        Loading,         
        RequestingSetup, 
        WaitingForSetup, 
        Rebuilding,      
        Done,            
        Failed,          
    }

    public ScanPhase CurrentPhase { get; private set; } = ScanPhase.Idle;
    public event System.Action<ScanPhase> OnScanPhaseChanged;
    public event System.Action<MRUKRoom> OnRoomReady;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Dummy Room")]
    [SerializeField] private bool forceDummyRoom = false;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = true;

    // Sub-components
    private RoomScanPipeline _scanPipeline;
    private RoomVisualizer _visualizer;
    private RoomObjectPlacer _objectPlacer;
    private DummyRoomBuilder _dummyRoomBuilder;
    private MRUK _mruk;

    void Awake()
    {
        _scanPipeline = GetComponent<RoomScanPipeline>();
        _visualizer = GetComponent<RoomVisualizer>();
        _objectPlacer = GetComponent<RoomObjectPlacer>();
        _dummyRoomBuilder = GetComponent<DummyRoomBuilder>();
    }

    void Start()
    {
        _mruk = MRUK.Instance;
        
        bool requiresDummyRoom = forceDummyRoom || !DeviceChecker.IsQuest3();

        if (requiresDummyRoom)
        {
            SetStatus("Device lacks MR depth sensors or dummy requested. Starting dummy room...");
            _dummyRoomBuilder.BuildDummyRoom();
            SetStatus("✅ Dummy room ready!");
            // Fire event with null or a dummy room context
            OnRoomReady?.Invoke(null); 
        }
        else
        {
            SetStatus("Starting room scan...");
            _scanPipeline.Initialize(_mruk);
            StartCoroutine(_scanPipeline.RunScan(SetStatus, OnScanSuccess, OnScanFailed));
        }
    }

    private void SetPhase(ScanPhase phase)
    {
        CurrentPhase = phase;
        OnScanPhaseChanged?.Invoke(phase);
        Log($"[Phase] {phase}");
    }

    private void OnScanSuccess(MRUKRoom room)
    {
        SetPhase(ScanPhase.Rebuilding);
        SetStatus("방 메시를 재구성하는 중...");
        _visualizer.BuildRoomVisuals(room);

        SetStatus("게임 오브젝트를 배치하는 중...");
        _objectPlacer.PlaceGameObjects(room);

        SetPhase(ScanPhase.Done);
        SetStatus($"✅ 완료!");
        OnRoomReady?.Invoke(room);
    }

    private void OnScanFailed()
    {
        SetPhase(ScanPhase.Failed);
    }

    private void SetStatus(string msg)
    {
        Log(msg);
        if (statusText != null) statusText.text = msg;
    }

    private void Log(string msg, bool isWarning = false)
    {
        if (!showDebugLog) return;
        if (isWarning) Debug.LogWarning($"[RoomManager] {msg}");
        else           Debug.Log($"[RoomManager] {msg}");
    }
}
