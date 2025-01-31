#nullable enable
using UnityEngine;
using Fusion;
using TMPro;
using System.Linq;
using System;
using UnityEngine.UI;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float _playerSpeed;
    [SerializeField] private AttackProjectile _prefabAttack;
    [SerializeField] private InventoryItem _inventoryItemPrefab;

    [Networked] private TickTimer _delay { get; set; }
    [Networked] public int Health { get; set; }
    
    /// <summary>
    /// Every point of armor provides 0.1% damage reduction
    /// </summary>
    [Networked] public int Armor { get; set; }
    [Networked] public int PlayerId { get; set; }
    [Networked] public Vector2 PlayerPosition { get; set; }

    // NetworkStruct needs to be a ref in order to get ref for modification rather than value copy
    [Networked] public ref WeaponNetworkStruct WeaponNetworkStruct  => ref MakeRef<WeaponNetworkStruct>();

    private TMP_Text _chatBox;
    private GameObject _inventoryScreen;
    private ChangeDetector _changeDetector;
    private Weapon? _weaponFromServer;
    private Image _healthBarBlood;

    private void Update()
    {
        if (Object.HasInputAuthority)
        {
            var newCameraPosition = Vector2.Lerp(Camera.main.transform.position, transform.position, Time.deltaTime / 0.1f);
            Camera.main.transform.position = new Vector3(newCameraPosition.x, newCameraPosition.y, -10f);

            _healthBarBlood.fillAmount = Health/100f;

            Weapon? playerWeapon = FindObjectsOfType<InventorySlot>(includeInactive: true).Where(slot => slot.IsEquipmentSlot && slot.EquipmentType == EquipmentType.MAIN_HAND).FirstOrDefault()?.InventoryItem?.Item as Weapon;
            if (playerWeapon?.ItemId != _weaponFromServer?.ItemId)
            {
                RPC_SendWeaponUpdate(TysonsHelpers.SerializeWeapon(playerWeapon));
                _weaponFromServer = playerWeapon;
            }

            RPC_SendArmorUpdate(GetCurrentArmorTotal());
        
            if (Input.GetKeyDown(KeyCode.T))
            {
                RPC_SendMessage("ggez");
            }
            
            if (Input.GetKeyDown(KeyCode.I))
            {
                _inventoryScreen.SetActive(!_inventoryScreen.activeSelf);
            }
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
    public void RPC_SendMessage(string message, RpcInfo info = default)
    {
        RPC_RelayMessage(message, info.Source);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, HostMode = RpcHostMode.SourceIsServer)]
    public void RPC_RelayMessage(string message, PlayerRef messageSource)
    {
        if (_chatBox == null)
        {
            _chatBox = FindObjectsOfType<TMP_Text>().First(text => text.gameObject.name == "Chat Box");
        }

        if (messageSource == Runner.LocalPlayer)
        {
            _chatBox.text += $"You said: {message}\n";
        }
        else
        {
            _chatBox.text += $"Someone said: {message}\n";
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
    public void RPC_SendWeaponUpdate(WeaponNetworkStruct newWeapon, RpcInfo info = default)
    {
        // I am the State Authority here, so I can update the Networked property
        WeaponNetworkStruct = newWeapon;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
    public void RPC_SendArmorUpdate(int armorAmount, RpcInfo info = default)
    {
        // I am the State Authority here, so I can update the Networked property
        Armor = armorAmount;
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority)
        {
            if (Health <= 0)
            {
                Runner.Despawn(Object);
            }

            // We are the server in here, so we need to update weapon
            _weaponFromServer = TysonsHelpers.DeserializeWeapon(WeaponNetworkStruct);
        }

        if (GetInput(out NetworkInputData data))
        {
            data.direction.Normalize();

            gameObject.GetComponent<Rigidbody2D>().MovePosition(gameObject.transform.position + _playerSpeed * data.direction * Runner.DeltaTime);
            gameObject.GetComponent<Rigidbody2D>().velocity = Vector2.zero;

            if (HasStateAuthority)
            {
                PlayerPosition = transform.position;
            }

            if (HasInputAuthority && !HasStateAuthority)
            {
                // If we are a non-host client and getting out of sync, then get location from Networked property
                if ((PlayerPosition - (Vector2)transform.position).magnitude > 1)
                {
                    gameObject.GetComponent<Rigidbody2D>().MovePosition(PlayerPosition);
                    gameObject.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
                }
            }

            // If we are state authority, we can spawn attacks for other players
            // These attacks will only be executed on host and NOT predicted on clients
            if (HasStateAuthority && _delay.ExpiredOrNotRunning(Runner)  && data.buttons.IsSet(NetworkInputData.MOUSEBUTTON0))
            {
                // Handle weapon attack
                if (_weaponFromServer != null && _weaponFromServer.FireRate != 0)
                {
                    // Limit shooting rate
                    _delay = TickTimer.CreateFromSeconds(Runner, 1f/_weaponFromServer.FireRate);

                    Runner.Spawn(_prefabAttack, transform.position, Quaternion.identity, Object.InputAuthority,
                    (runner, o) =>
                    {
                        // This callback will be called after instantiating object but before it is synchronized
                        _weaponFromServer.Attack(o);
                        o.GetComponent<AttackProjectile>().ShooterId = PlayerId;
                        o.GetComponent<AttackProjectile>().Direction = (data.clickLocation - (Vector2)transform.position).normalized;
                        o.GetComponent<AttackProjectile>().Color = Color.blue;
                    });
                }
                else
                {
                    _delay = TickTimer.CreateFromSeconds(Runner, 1);
                }
            }
        }
    }

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        Health = 10000;

        if (Object.HasInputAuthority)
        {
            // When a player first joins we run this code
            FindObjectOfType<Canvas>().transform.Find("Health Bar").gameObject.SetActive(true);
            FindObjectOfType<Canvas>().transform.Find("Loading").gameObject.SetActive(false);
            _inventoryScreen = FindObjectOfType<Canvas>().transform.Find("Inventory Screen").gameObject;
            _healthBarBlood = FindObjectsOfType<Image>(includeInactive: true).First(e => e.gameObject.name == "Health Bar Blood");

            FindObjectsOfType<InventoryItem>(includeInactive: true).ForEach(item => Destroy(item.gameObject));

            // Testing code - spawn two swords in player's inventory (client-side only)
            var sampleWeapon = Instantiate(_inventoryItemPrefab, _inventoryScreen.transform.Find("Items"));
            sampleWeapon.Item = new Weapon() { Name = "My First Sword", Type = "1H Sword", FireRate = 2, MinDamage = 3, MaxDamage = 10, Range = 7, Speed = 10, ShotType = ShotType.STRAIGHT, EquipmentType = EquipmentType.MAIN_HAND };
            sampleWeapon.InventorySlotNumber = 7;
            var sampleBetterWeapon = Instantiate(_inventoryItemPrefab, _inventoryScreen.transform.Find("Items"));
            sampleBetterWeapon.Item = new Weapon() { Name = "A Better Sword", Type = "1H Sword", FireRate = 4, MinDamage = 10, MaxDamage = 20, Range = 6, Speed = 8, ShotType = ShotType.STRAIGHT, EquipmentType = EquipmentType.MAIN_HAND };
            sampleBetterWeapon.InventorySlotNumber = 8;

            _inventoryScreen.gameObject.SetActive(false);
        }
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(Health):
                    GetComponentInChildren<SpriteRenderer>().color = Color.red;
                    break;
            }
        }

        GetComponentInChildren<SpriteRenderer>().color = Color.Lerp(GetComponentInChildren<SpriteRenderer>().color, Color.blue, Time.deltaTime / 0.4f);
    }

    public void SpawnLootForSelf()
    {
        var newItem = Instantiate(_inventoryItemPrefab, _inventoryScreen.transform.Find("Items"));
        
        var random = new System.Random();
        var randomInt = random.Next(0, 5);

        switch (randomInt)
        {
            case 0:
                newItem.Item = Weapon.GenerateWeapon();
                break;
            case 1: 
                newItem.Item = new Armor(EquipmentType.HEAD, "Iron Helm", "Head", random.Next(0, 101));
                break;
            case 2: 
                newItem.Item = new Armor(EquipmentType.TORSO, "Iron Chestplate", "Torso", random.Next(0, 101));
                break;
            case 3: 
                newItem.Item = new Armor(EquipmentType.LEGS, "Leather Greaves", "Legs", random.Next(0, 101));
                break;
            case 4: 
                newItem.Item = new Armor(EquipmentType.FEET, "Leather Boots", "Feet", random.Next(0, 101));
                break;
            default:
                break;
        }
        
        newItem.InventorySlotNumber = FindObjectsOfType<InventorySlot>(includeInactive: true).Where(slot => !slot.IsEquipmentSlot && slot.InventoryItem == null).Min(slot => slot.SlotNumber);
}

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (HasStateAuthority)
        {
            if (other.TryGetComponent(out AttackProjectile attackProjectile))
            {
                if (attackProjectile.ShooterId != PlayerId)
                {
                    int damageAmount = (int)(attackProjectile.Damage * (1f - (Armor / 1000f)));
                    Health -= damageAmount;
                    Debug.Log($"Player with {Armor} armor took {attackProjectile.Damage} reduced to {damageAmount} damage and now has {Health} health!");

                    Runner.Despawn(attackProjectile.Object);
                }
            }
        }
    }

    private int GetCurrentArmorTotal()
    {
        int currentArmor = 0;

        // Get Head Armor
        Armor? currentHead = FindObjectsOfType<InventorySlot>(includeInactive: true).Where(slot => slot.IsEquipmentSlot && slot.EquipmentType == EquipmentType.HEAD).FirstOrDefault()?.InventoryItem?.Item as Armor;
        currentArmor += currentHead?.ArmorAmount ?? 0;

        // Get Torso Armor
        Armor? currentTorso = FindObjectsOfType<InventorySlot>(includeInactive: true).Where(slot => slot.IsEquipmentSlot && slot.EquipmentType == EquipmentType.TORSO).FirstOrDefault()?.InventoryItem?.Item as Armor;
        currentArmor += currentTorso?.ArmorAmount ?? 0;

        // Get Legs Armor
        Armor? currentLegs = FindObjectsOfType<InventorySlot>(includeInactive: true).Where(slot => slot.IsEquipmentSlot && slot.EquipmentType == EquipmentType.LEGS).FirstOrDefault()?.InventoryItem?.Item as Armor;
        currentArmor += currentLegs?.ArmorAmount ?? 0;

        // Get Legs Armor
        Armor? currentFeet = FindObjectsOfType<InventorySlot>(includeInactive: true).Where(slot => slot.IsEquipmentSlot && slot.EquipmentType == EquipmentType.FEET).FirstOrDefault()?.InventoryItem?.Item as Armor;
        currentArmor += currentFeet?.ArmorAmount ?? 0;

        return currentArmor;
    }
}
 