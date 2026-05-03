using UnityEngine;

public class Battery : MonoBehaviour
{
    [HideInInspector] public bool isSnapped = false;

    private Rigidbody rb;
    private Renderer rend;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rend = GetComponentInChildren<Renderer>();
    }

    // Snap 시작 시 호출 - Grab만 끊고 물리는 슬롯 스크립트가 Lerp로 이동
    public void BeginSnap()
    {
        isSnapped = true;

        var grabbable = GetComponent<Oculus.Interaction.Grabbable>();
        if (grabbable != null) grabbable.enabled = false;

        var rayInteractable = GetComponent<Oculus.Interaction.RayInteractable>();
        if (rayInteractable != null) rayInteractable.enabled = false;

        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    // Snap 완료 시 호출
    public void SetCharged()
    {
        if (rend != null)
        {
            // 반투명 초록색
            Color green = new Color(0f, 1f, 0f, 0.85f);
            rend.material.color = green;
        }
        Debug.Log("🔋 충전 완료!");
    }
}