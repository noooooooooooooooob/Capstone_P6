using UnityEngine;

public class BatterySlot : MonoBehaviour
{
    [Header("Snap 설정")]
    public float snapDistance = 3f;
    public float snapSpeed = 8f;

    [Header("연결")]
    public ControlPanelManager controlPanel; // Inspector에서 연결

    private Renderer slotRenderer; // 배터리 대신 슬롯이 초록색
    private bool isOccupied = false;
    private Battery snappedBattery = null;
    private bool isSnapping = false;

    void Awake()
    {
        slotRenderer = GetComponent<Renderer>();
    }

    void Update()
    {
        if (isSnapping && snappedBattery != null)
        {
            snappedBattery.transform.position = Vector3.Lerp(
                snappedBattery.transform.position,
                transform.position,
                Time.deltaTime * snapSpeed
            );
            snappedBattery.transform.rotation = Quaternion.Lerp(
                snappedBattery.transform.rotation,
                transform.rotation,
                Time.deltaTime * snapSpeed
            );

            if (Vector3.Distance(snappedBattery.transform.position, transform.position) < 0.005f)
            {
                snappedBattery.transform.position = transform.position;
                snappedBattery.transform.rotation = transform.rotation;
                isSnapping = false;
                OnSnapComplete();
            }
            return;
        }

        if (isOccupied) return;

        Battery[] batteries = FindObjectsByType<Battery>(FindObjectsSortMode.None);
        foreach (var battery in batteries)
        {
            if (battery.isSnapped) continue;

            float dist = Vector3.Distance(transform.position, battery.transform.position);
            if (dist < snapDistance)
            {
                StartSnap(battery);
                break;
            }
        }
    }

    void StartSnap(Battery battery)
    {
        isOccupied = true;
        isSnapping = true;
        snappedBattery = battery;
        battery.BeginSnap();
    }

    void OnSnapComplete()
    {
        // 슬롯 초록색으로
        if (slotRenderer != null)
            slotRenderer.material.color = new Color(0f, 1f, 0f, 0.5f);

        // 컨트롤 패널 배터리 100으로
        if (controlPanel != null)
        {
            controlPanel.battery = 100f;
            controlPanel.UpdateUI();
            Debug.Log("🔋 배터리 슬롯 삽입 → 배터리 100% 충전!");
        }
        else
        {
            Debug.LogWarning("ControlPanel이 연결되지 않았습니다!");
        }
    }
}