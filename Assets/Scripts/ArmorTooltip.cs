using System.Linq;
using TMPro;
using UnityEngine;

public class ArmorTooltip : MonoBehaviour
{
    private TextMeshProUGUI _nameText;
    private TextMeshProUGUI _typeText;
    private TextMeshProUGUI _armorText;

    public Armor Armor
    {
        set
        {
            if (_armor == value)
            {
                return;
            }

            _armor = value;

            _nameText.text = $"{_armor.Name}";
            _typeText.text = $"{_armor.Type}";
            _armorText.text = $"{_armor.ArmorAmount} armor";
        }
    }
    private Armor _armor;

    // Start is called before the first frame update
    void Start() { }

    // Update is called once per frame
    void Update() { }

    void Awake()
    {
        var textBlocks = FindObjectsOfType<TextMeshProUGUI>();
        _nameText = textBlocks.First(textBlock => textBlock.transform.name == "Name");
        _typeText = textBlocks.First(textBlock => textBlock.transform.name == "Type");
        _armorText = textBlocks.First(textBlock => textBlock.transform.name == "Armor");
    }
}
