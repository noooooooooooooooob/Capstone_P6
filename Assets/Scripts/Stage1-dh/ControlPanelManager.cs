using UnityEngine;
using TMPro; // TextMeshPro를 사용하기 위해 필요
using UnityEngine.UI;
using System.Diagnostics;

public class ControlPanelManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI batteryText;
    public TextMeshProUGUI stabilityText;
    public Button stabilizeButton;

    [Header("Values")]
    public float battery = 100f;
    public float stability = 50f;

    [Header("Settings (Per Second)")]
    public float batteryDrainRate = 5f;    // 초당 5% 감소
    public float stabilityGainRate = 8f;   // 초당 8% 증가

    public float clickedTimes = 0f;
    private bool isStabilizing = false;

    void Start()
    {
        
        UpdateUI();
    }

    void Update()
    {
        if (isStabilizing)
        {
            // 배터리가 있을 때만 작동
            if (battery > 0 && stability <100)
            {
                battery -= batteryDrainRate * Time.deltaTime;
                stability += stabilityGainRate * Time.deltaTime;

                // 값 범위 제한 (0~100)
                battery = Mathf.Clamp(battery, 0, 100);
                stability = Mathf.Clamp(stability, 0, 100);
            }
            else
            {
                isStabilizing = false; // 배터리 없으면 중단
                UnityEngine.Debug.Log("Stabilizing 상태: " + isStabilizing + clickedTimes++);
                UpdateUI();
            }

            UpdateUI();
        }
    }

    public void ToggleStabilize()
    {
        isStabilizing = !isStabilizing;
        UnityEngine.Debug.Log("Stabilizing 상태: " + isStabilizing + clickedTimes++);
        
        UpdateUI();
    }

    public void UpdateUI()
    {
        batteryText.text = $"Battery: {battery:F0}%";
        stabilityText.text = $"Stability: {stability:F0}%";
        
        batteryText.color = isStabilizing ? Color.red : Color.white;
        stabilityText.color = isStabilizing ? Color.blue : Color.white;
    }
}