using System.Linq;
using TMPro;
using UnityEngine;

public class WeaponTooltip : MonoBehaviour
{
    private TextMeshProUGUI _nameText;
    private TextMeshProUGUI _typeText;
    private TextMeshProUGUI _damageText;
    private TextMeshProUGUI _rangeText;

    public Weapon Weapon
    {
        set
        {
            if (_weapon == value)
            {
                return;
            }

            _weapon = value;

            float avgDmg = (_weapon.MinDamage + _weapon.MaxDamage) / 2f * _weapon.FireRate;
            _nameText.text = $"{_weapon.Name}";
            _typeText.text = $"{_weapon.Type}";
            _damageText.text = $"{_weapon.MinDamage}-{_weapon.MaxDamage} damage, {_weapon.FireRate} fire rate ({avgDmg} dps)";
            _rangeText.text = $"Range of {_weapon.Range}";
        }
    }
    private Weapon _weapon;

    // Start is called before the first frame update
    void Start() { }

    // Update is called once per frame
    void Update() { }

    void Awake()
    {
        var textBlocks = FindObjectsOfType<TextMeshProUGUI>();
        _nameText = textBlocks.First(textBlock => textBlock.transform.name == "Name");
        _typeText = textBlocks.First(textBlock => textBlock.transform.name == "Type");
        _damageText = textBlocks.First(textBlock => textBlock.transform.name == "Damage");
        _rangeText = textBlocks.First(textBlock => textBlock.transform.name == "Range");
    }
}
