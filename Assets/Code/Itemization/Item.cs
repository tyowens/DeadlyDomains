using System;

public abstract class Item
{
    public ItemRarity ItemRarity;
    public Guid ItemId = Guid.NewGuid();
    public EquipmentType EquipmentType;
}

public enum ItemRarity
{
    NONE = 0,
    COMMON = 1,
    UNCOMMON = 2,
    RARE = 3
}