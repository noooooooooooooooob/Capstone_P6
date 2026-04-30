using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class VRKeyboard : MonoBehaviour
{
    [SerializeField] TMP_InputField targetInput;
    [SerializeField] Transform keyContainer;
    [SerializeField] Button keyButtonPrefab;

    [SerializeField] string[] rows = {
        "1234567890",
        "QWERTYUIOP",
        "ASDFGHJKL",
        "ZXCVBNM"
    };

    [SerializeField] float keyWidth = 60f;
    [SerializeField] float keyHeight = 60f;
    [SerializeField] float spacing = 5f;

    public UnityEvent<string> OnSubmit;

    void Start()
    {
        ConfigureContainer();

        foreach (var row in rows)
        {
            var rowTf = CreateRow();
            foreach (var c in row)
            {
                var key = c.ToString();
                AddButton(rowTf, key, keyWidth, () => Append(key));
            }
        }

        var specialRowTf = CreateRow();
        AddButton(specialRowTf, "Space", keyWidth * 4, () => Append(" "));
        AddButton(specialRowTf, "Back",  keyWidth * 2, Backspace);
        AddButton(specialRowTf, "Enter", keyWidth * 2, Submit);
    }

    void ConfigureContainer()
    {
        var vlg = keyContainer.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = keyContainer.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = spacing;
    }

    Transform CreateRow()
    {
        var rowGo = new GameObject("Row", typeof(RectTransform));
        rowGo.transform.SetParent(keyContainer, false);

        var rt = (RectTransform)rowGo.transform;
        rt.sizeDelta = new Vector2(0, keyHeight);

        var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = spacing;

        return rowGo.transform;
    }

    void AddButton(Transform parent, string label, float width, UnityAction action)
    {
        var btn = Instantiate(keyButtonPrefab, parent);
        var rt = (RectTransform)btn.transform;
        rt.sizeDelta = new Vector2(width, keyHeight);

        var text = btn.GetComponentInChildren<TMP_Text>();
        if (text != null) text.text = label;
        btn.onClick.AddListener(action);
    }

    void Append(string s) => targetInput.text += s;

    void Backspace()
    {
        if (targetInput.text.Length > 0)
            targetInput.text = targetInput.text[..^1];
    }

    void Submit()
    {
        var msg = targetInput.text;
        if (string.IsNullOrWhiteSpace(msg)) return;
        OnSubmit?.Invoke(msg);
        targetInput.text = "";
    }
}
