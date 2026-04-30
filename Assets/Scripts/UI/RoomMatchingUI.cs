using System.Threading.Tasks;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RoomMatchingUI : MonoBehaviour
{
    [SerializeField] NetworkRunner runnerPrefab;
    [SerializeField] TMP_InputField roomCodeInput;
    [SerializeField] TMP_Text statusText;
    [SerializeField] TMP_Text roomTokenText;
    [SerializeField] int maxPlayers = 2;

    NetworkRunner _activeRunner;

    void Awake()
    {
        if (runnerPrefab == null)
        {
            runnerPrefab = FindFirstObjectByType<NetworkRunner>();
        }
    }

    public async void MakeRoom()
    {
        var code = NormalizeCode(roomCodeInput != null ? roomCodeInput.text : null);
        if (string.IsNullOrEmpty(code))
        {
            SetStatus("Enter a room code first.");
            return;
        }

        SetStatus($"Creating room {code}...");
        await StartSession(GameMode.Host, code);
    }

    public async void JoinRoom()
    {
        var code = NormalizeCode(roomCodeInput != null ? roomCodeInput.text : null);
        if (string.IsNullOrEmpty(code))
        {
            SetStatus("Enter a room code first.");
            return;
        }

        SetStatus($"Joining {code}...");
        await StartSession(GameMode.Client, code);
    }

    public void LeaveRoom()
    {
        if (_activeRunner != null && _activeRunner.IsRunning)
        {
            _activeRunner.Shutdown();
        }
        _activeRunner = null;
        SetStatus("Left room.");
        SetRoomToken(string.Empty);
    }

    async Task StartSession(GameMode mode, string sessionName)
    {
        if (runnerPrefab == null)
        {
            SetStatus("NetworkRunner not found. Add Network Manager block.");
            return;
        }

        if (_activeRunner != null && _activeRunner.IsRunning)
        {
            _activeRunner.Shutdown();
            _activeRunner = null;
        }

        runnerPrefab.gameObject.SetActive(false);
        _activeRunner = Instantiate(runnerPrefab);
        _activeRunner.name = $"NetworkRunner ({mode})";
        _activeRunner.gameObject.SetActive(true);
        DontDestroyOnLoad(_activeRunner);

        var result = await _activeRunner.StartGame(new StartGameArgs
        {
            GameMode = mode,
            SessionName = sessionName,
            PlayerCount = maxPlayers,
            Scene = GetSceneInfo(),
        });

        if (result.Ok)
        {
            SetStatus(mode == GameMode.Host ? "Room created. Share the code." : "Joined room.");
            SetRoomToken(sessionName);
        }
        else
        {
            SetStatus($"Failed: {result.ShutdownReason} {result.ErrorMessage}");
            if (_activeRunner != null) Destroy(_activeRunner.gameObject);
            _activeRunner = null;
        }
    }

    static NetworkSceneInfo GetSceneInfo()
    {
        var info = new NetworkSceneInfo();
        var active = SceneManager.GetActiveScene();
        if (active.buildIndex >= 0 && active.buildIndex < SceneManager.sceneCountInBuildSettings)
        {
            info.AddSceneRef(SceneRef.FromIndex(active.buildIndex), LoadSceneMode.Additive);
        }
        return info;
    }

    static string NormalizeCode(string raw) =>
        string.IsNullOrWhiteSpace(raw) ? null : raw.Trim().ToUpperInvariant();

    void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }

    void SetRoomToken(string token)
    {
        if (roomTokenText != null) roomTokenText.text = token;
    }
}
