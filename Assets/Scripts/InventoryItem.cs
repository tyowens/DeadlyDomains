#nullable enable
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryItem : MonoBehaviour
{
    [SerializeField] public Sprite swordSprite;
    [SerializeField] public Sprite helmSprite;
    [SerializeField] public Sprite chestPlateSprite;
    [SerializeField] public Sprite pantsSprite;
    [SerializeField] public Sprite bootsSprite;

    public int InventorySlotNumber
    {
        get { return _inventorySlotNumber; }
        set
        {
            _inventorySlotNumber = value;
            MoveToSlot();
        }
    }
    private int _inventorySlotNumber;

    public bool IsBeingDragged
    {
        get { return _isBeingDragged; }
        set
        {
            if (_isBeingDragged == value) { return; }

            _isBeingDragged = value;

            if (!value)
            {
                // Drop logic
                var raycastInventorySlot = BasicSpawner.GetEventSystemRaycastResults().Where(e => e.gameObject.GetComponent<InventorySlot>()).FirstOrNull();
                if (raycastInventorySlot != null)
                {
                    var destinationSlot = ((RaycastResult)raycastInventorySlot).gameObject.GetComponent<InventorySlot>();

                    // If none dragging to non-specific slot and slot types don't match, then do not move
                    if (Item != null
                        && destinationSlot.EquipmentType != EquipmentType.NONE
                        && destinationSlot.EquipmentType != Item.EquipmentType)
                    {
                        MoveToSlot();
                        return;
                    }

                    // Remove item reference from old InventorySlot
                    InventorySlot sourceSlot = FindObjectsOfType<InventorySlot>(includeInactive: true).Where(e => e.SlotNumber == InventorySlotNumber).FirstOrDefault();

                    // If there is something in the slot we are dropping in, we need to move it into the source slot of this drag
                    if (destinationSlot.InventoryItem != null)
                    {
                        destinationSlot.InventoryItem.InventorySlotNumber = sourceSlot.SlotNumber;
                    }

                    InventorySlotNumber = destinationSlot.SlotNumber;
                }
                else
                {
                    MoveToSlot();
                }
            }
        }
    }
    private bool _isBeingDragged;

    public Item? Item
    {
        get { return _item; }
        set
        {
            _item = value;

            if (_item != null)
            {
                if (_item.EquipmentType == EquipmentType.MAIN_HAND)
                {
                    GetComponent<UnityEngine.UI.Image>().sprite = swordSprite;
                }
                else if (_item.EquipmentType == EquipmentType.HEAD)
                {
                    GetComponent<UnityEngine.UI.Image>().sprite = helmSprite;
                }
                else if (_item.EquipmentType == EquipmentType.TORSO)
                {
                    GetComponent<UnityEngine.UI.Image>().sprite = chestPlateSprite;
                }
                else if (_item.EquipmentType == EquipmentType.LEGS)
                {
                    GetComponent<UnityEngine.UI.Image>().sprite = pantsSprite;
                }
                else if (_item.EquipmentType == EquipmentType.FEET)
                {
                    GetComponent<UnityEngine.UI.Image>().sprite = bootsSprite;
                }
            }
        }
    }
    private Item? _item;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (IsBeingDragged)
        {
            transform.position = Input.mousePosition;
        }
    }

    private void MoveToSlot()
    {
        var invScreen = FindObjectOfType<Canvas>().transform.Find("Inventory Screen");
        var invSlots = invScreen.GetComponentsInChildren<InventorySlot>(includeInactive: true);
        var inventorySlot = invScreen.GetComponentsInChildren<InventorySlot>(includeInactive: true).FirstOrDefault(e => e.SlotNumber == _inventorySlotNumber);
        if (inventorySlot != null)
        {
            transform.position = inventorySlot.transform.position;

            // Clear out references to this item on old slots
            FindObjectsOfType<InventorySlot>().Where(slot => slot.InventoryItem?.Item?.ItemId == this.Item?.ItemId).ForEach(slot => slot.SetInventoryItem(null));

            inventorySlot.SetInventoryItem(this);
        }
    }
}
