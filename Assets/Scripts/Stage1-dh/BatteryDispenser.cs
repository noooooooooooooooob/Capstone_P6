// BatteryDispenser.cs
using UnityEngine;

public class BatteryDispenser : MonoBehaviour
{
    [Header("배터리 설정")]
    public GameObject batteryPrefab;  // GrabTestBattery 프리팹
    public Transform spawnPoint;      // 큐브 앞면 위치
    public float ejectForce = 3f;

    [Header("쿨다운")]
    public float cooldown = 1.5f;
    private float lastSpawnTime = -999f;

    public void DispenseBattery()
    {
        if (Time.time - lastSpawnTime < cooldown) return;
        lastSpawnTime = Time.time;

        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position + transform.forward * 0.5f;
        GameObject battery = Instantiate(batteryPrefab, pos, Quaternion.identity);

        Rigidbody rb = battery.GetComponent<Rigidbody>();
        if (rb != null)
            rb.AddForce(transform.forward * ejectForce, ForceMode.Impulse);

        Debug.Log("🔋 배터리 배출!");
    }
}