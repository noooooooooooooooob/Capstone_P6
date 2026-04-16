using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Puzzle;

namespace Puzzle
{
    /// <summary>
    /// EnvironmentScanner 결과를 받아 적절한 퍼즐 모드를 활성화합니다.
    /// 색-숫자 매핑과 정답 코드를 관리합니다.
    /// </summary>
    public class PuzzleManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private EnvironmentScanner environmentScanner;
        [SerializeField] private ChairPuzzle chairPuzzle;
        [SerializeField] private DeskPuzzle deskPuzzle;
        [SerializeField] private EmptyRoomPuzzle emptyRoomPuzzle;
        [SerializeField] private LockController lockController;

        [Header("Puzzle Settings")]
        [SerializeField] private int codeLength = 4;

        // 색-숫자 매핑 (B가 들고 있는 답지와 동일)
        private Dictionary<PuzzleColor, int> colorToNumber = new Dictionary<PuzzleColor, int>
        {
            { PuzzleColor.Red,    1 },
            { PuzzleColor.Green,  2 },
            { PuzzleColor.Blue,   3 },
            { PuzzleColor.Yellow, 4 },
            { PuzzleColor.Purple, 5 }
        };

        // 이번 퍼즐의 색 순서 (A가 보는 것)
        private List<PuzzleColor> colorSequence = new List<PuzzleColor>();
        private string correctCode = "";

        public IReadOnlyList<PuzzleColor> ColorSequence => colorSequence;
        public string CorrectCode => correctCode;
        public Dictionary<PuzzleColor, int> ColorToNumber => colorToNumber;

        private void Start()
        {
            if (environmentScanner == null)
                environmentScanner = FindFirstObjectByType<EnvironmentScanner>();

            StartCoroutine(WaitAndInit());
        }

private IEnumerator WaitAndInit()
        {
            float timeout = 10f;
            float elapsed = 0f;
            while ((environmentScanner == null || !environmentScanner.IsReady) && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (environmentScanner == null || !environmentScanner.IsReady)
            {
                Debug.LogWarning("[PuzzleManager] EnvironmentScanner timeout. Defaulting to EmptyRoom.");
                GeneratePuzzle();
                ActivatePuzzleMode(EnvironmentType.EmptyRoom);
            }
            else
            {
                GeneratePuzzle();
                ActivatePuzzleMode(environmentScanner.DetectedType);
            }
        }

        private void GeneratePuzzle()
        {
            colorSequence.Clear();
            correctCode = "";

            PuzzleColor[] allColors = (PuzzleColor[])System.Enum.GetValues(typeof(PuzzleColor));

            for (int i = 0; i < codeLength; i++)
            {
                PuzzleColor picked = allColors[Random.Range(0, allColors.Length)];
                colorSequence.Add(picked);
                correctCode += colorToNumber[picked].ToString();
            }

            Debug.Log($"[PuzzleManager] Code sequence: {string.Join(", ", colorSequence)} → {correctCode}");

            lockController?.SetCorrectCode(correctCode);
        }

        private void ActivatePuzzleMode(EnvironmentType type)
        {
            chairPuzzle?.gameObject.SetActive(false);
            deskPuzzle?.gameObject.SetActive(false);
            emptyRoomPuzzle?.gameObject.SetActive(false);

            switch (type)
            {
                case EnvironmentType.Chair:
                    Debug.Log("[PuzzleManager] Activating Chair Puzzle");
                    chairPuzzle?.gameObject.SetActive(true);
                    chairPuzzle?.Initialize(colorSequence);
                    break;

                case EnvironmentType.Desk:
                    Debug.Log("[PuzzleManager] Activating Desk Puzzle");
                    deskPuzzle?.gameObject.SetActive(true);
                    deskPuzzle?.Initialize(colorSequence);
                    break;

                case EnvironmentType.EmptyRoom:
                default:
                    Debug.Log("[PuzzleManager] Activating EmptyRoom Puzzle");
                    emptyRoomPuzzle?.gameObject.SetActive(true);
                    emptyRoomPuzzle?.Initialize(colorSequence);
                    break;
            }
        }
    }
}
