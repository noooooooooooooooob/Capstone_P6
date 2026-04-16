using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Puzzle;

namespace Puzzle
{
    /// <summary>
    /// 숫자 키패드 UI + 정답 검증.
    /// 버튼 클릭 시 파란 하이라이트 → 0.15초 후 원래 색으로 복귀.
    /// Quest 컨트롤러 Ray로 클릭 가능 (OVRInputModule 또는 기본 EventSystem 모두 지원).
    /// </summary>
    public class LockController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI inputDisplay;
        [SerializeField] private TextMeshProUGUI feedbackText;
        [SerializeField] private Button[] numberButtons;   // index 0~9
        [SerializeField] private Button deleteButton;
        [SerializeField] private Button confirmButton;

        [Header("Settings")]
        [SerializeField] private int maxInputLength = 4;

        [Header("Colors")]
        [SerializeField] private Color normalColor    = new Color(0.25f, 0.25f, 0.25f);
        [SerializeField] private Color highlightColor = new Color(0.15f, 0.45f, 0.85f); // 파란색
        [SerializeField] private Color correctColor   = new Color(0.15f, 0.7f, 0.2f);
        [SerializeField] private Color wrongColor     = new Color(0.75f, 0.15f, 0.15f);

        private string correctCode = "";
        private string currentInput = "";

        private void Start()
        {
            SetupButtons();
            UpdateDisplay();
            SetFeedback("", Color.white);
        }

        public void SetCorrectCode(string code)
        {
            correctCode = code;
            Debug.Log($"[LockController] Correct code: {correctCode}");
        }

        // ── 버튼 세팅 ────────────────────────────────────────
        private void SetupButtons()
        {
            for (int i = 0; i < numberButtons.Length; i++)
            {
                if (numberButtons[i] == null) continue;
                int num = i;
                numberButtons[i].onClick.AddListener(() => OnNumberPressed(num.ToString()));
                SetButtonColor(numberButtons[i], normalColor);
            }

            if (deleteButton != null)
            {
                deleteButton.onClick.AddListener(OnDeletePressed);
                SetButtonColor(deleteButton, new Color(0.55f, 0.15f, 0.15f));
            }
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(OnConfirmPressed);
                SetButtonColor(confirmButton, new Color(0.15f, 0.55f, 0.15f));
            }
        }

        // ── 입력 처리 ─────────────────────────────────────────
        private void OnNumberPressed(string num)
        {
            if (currentInput.Length >= maxInputLength) return;

            int idx;
            if (int.TryParse(num, out idx) && idx < numberButtons.Length && numberButtons[idx] != null)
                StartCoroutine(FlashButton(numberButtons[idx]));

            currentInput += num;
            UpdateDisplay();
            SetFeedback("", Color.white);
        }

        private void OnDeletePressed()
        {
            if (currentInput.Length == 0) return;
            StartCoroutine(FlashButton(deleteButton));
            currentInput = currentInput[..^1];
            UpdateDisplay();
            SetFeedback("", Color.white);
        }

        private void OnConfirmPressed()
        {
            if (currentInput.Length == 0) return;
            StartCoroutine(FlashButton(confirmButton));
            CheckCode();
        }

        private void CheckCode()
        {
            if (currentInput == correctCode)
            {
                SetFeedback("OPEN!", correctColor);
                Debug.Log("[LockController] Correct!");
                SetAllButtonsInteractable(false);
            }
            else
            {
                SetFeedback($"Wrong: {currentInput}", wrongColor);
                currentInput = "";
                UpdateDisplay();
            }
        }

        // ── 하이라이트 ────────────────────────────────────────
        private System.Collections.IEnumerator FlashButton(Button btn)
        {
            if (btn == null) yield break;
            SetButtonColor(btn, highlightColor);
            yield return new WaitForSeconds(0.15f);

            // 원래 색 복원 (DEL/OK 버튼은 고유색 유지)
            if (btn == deleteButton)
                SetButtonColor(btn, new Color(0.55f, 0.15f, 0.15f));
            else if (btn == confirmButton)
                SetButtonColor(btn, new Color(0.15f, 0.55f, 0.15f));
            else
                SetButtonColor(btn, normalColor);
        }

        private void SetButtonColor(Button btn, Color color)
        {
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = color;
        }

        private void SetAllButtonsInteractable(bool value)
        {
            foreach (var btn in numberButtons)
                if (btn != null) btn.interactable = value;
            if (deleteButton != null) deleteButton.interactable = value;
            if (confirmButton != null) confirmButton.interactable = value;
        }

        // ── 디스플레이 ────────────────────────────────────────
        private void UpdateDisplay()
        {
            if (inputDisplay == null) return;
            string masked = currentInput.PadRight(maxInputLength, '_');
            inputDisplay.text = string.Join(" ", masked.ToCharArray());
        }

        private void SetFeedback(string msg, Color color)
        {
            if (feedbackText == null) return;
            feedbackText.text = msg;
            feedbackText.color = color;
        }

        [ContextMenu("Test Correct Code")]
        private void TestCorrectCode()
        {
            currentInput = correctCode;
            CheckCode();
        }
    }
}
