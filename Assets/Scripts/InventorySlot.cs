#nullable enable
using UnityEngine;

public class InventorySlot : MonoBehaviour
{
    public int SlotNumber
    {
        get { return _slotNumber; }
    }
    [SerializeField] private int _slotNumber;

    [SerializeField] public bool IsEquipmentSlot;
    [SerializeField] public EquipmentType EquipmentType;

    public InventoryItem? InventoryItem
    {
        get { return _inventoryItem; }
    }
    private InventoryItem? _inventoryItem;

    // Start is called before the first frame update
    void Start()
    {
        _inventoryItem = null;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ApplyEquipmentBenefit(ref int damage)
    {
        if (InventoryItem != null)
        {
            damage *= 2;
        }
    }

    public void SetInventoryItem(InventoryItem? item)
    {
        _inventoryItem = item;
    }
}

public enum EquipmentType 
{
    NONE = 0,
    MAIN_HAND = 1,
    OFF_HAND = 2,
    HEAD = 3,
    TORSO = 4,
    LEGS = 5,
    FEET = 6
}