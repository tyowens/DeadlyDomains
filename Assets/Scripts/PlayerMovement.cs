#nullable enable
using UnityEngine;
using Fusion;
using TMPro;
using System.Linq;
using System;
using UnityEngine.UI;
using System.Collections.Generic;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float _playerSpeed;
    [SerializeField] private AttackProjectile _prefabAttack;
    [SerializeField] private InventoryItem _inventoryItemPrefab;

    [Networked] private TickTimer _attackDelay { get; set; }
    [Networked] private TickTimer _selfHealDelay { get; set; }
    [Networked] private TickTimer _outOfCombatDelay { get; set; }

    [Networked]
    [OnChangedRender(nameof(LevelChanged))]
    public int Level { get; set; }

    [Networked]
    [OnChangedRender(nameof(XpChanged))]
    public int KillsUntilLevelUp { get; set; }

    [Networked]
    [OnChangedRender(nameof(HealthChanged))]
    public int Health { get; set; }
    [Networked] public int MaxHealth { get; set; }
    
    /// <summary>
    /// Every point of armor provides 0.1% damage reduction
    /// </summary>
    [Networked] public int Armor { get; set; }
    [Networked] public int PlayerId { get; set; }
    [Networked] public Vector2 PlayerPosition { get; set; }
    [Networked] public Color PlayerColor { get; set; }
    [Networked] public Vector2 WalkingDirection { get; set; }

    // NetworkStruct needs to be a ref in order to get ref for modification rather than value copy
    [Networked] public ref WeaponNetworkStruct WeaponNetworkStruct  => ref MakeRef<WeaponNetworkStruct>();

    private TMP_Text _chatBox;
    private GameObject _inventoryScreen;
    private Weapon? _weaponFromServer;
    private Image _healthBarBlood;
    private Animator _animator;

    private void Update()
    {
        // Everyone do this!
        _animator.SetBool("IsMoving", WalkingDirection.magnitude > 0);
        _animator.SetFloat("HorizontalInput", WalkingDirection.x);
        _animator.SetFloat("VerticalInput", WalkingDirection.y);
        if (WalkingDirection.magnitude > 0)
        {
            _animator.SetFloat("LastHorizontalInput", WalkingDirection.x);
            _animator.SetFloat("LastVerticalInput", WalkingDirection.y);
        }

        if (Object.HasInputAuthority)
        {
            Camera.main.transform.SetParent(transform);
            Camera.main.transform.localPosition = new Vector3(0f, 0f, -10f);

            _healthBarBlood.fillAmount = (float)Health / MaxHealth;

            Weapon? playerWeapon = FindObjectsOfType<InventorySlot>(includeInactive: true).Where(slot => slot.IsEquipmentSlot && slot.EquipmentType == EquipmentType.MAIN_HAND).FirstOrDefault()?.InventoryItem?.Item as Weapon;
            if (playerWeapon?.ItemId != _weaponFromServer?.ItemId || (playerWeapon == null && _weaponFromServer != null))
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

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _inventoryScreen.SetActive(false);
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

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
    public void RPC_SendColorUpdate(Color color, RpcInfo info = default)
    {
        // I am the State Authority here, so I can update the Networked property
        PlayerColor = color;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
    public void RPC_SendWalkingDirectionUpdate(Vector2 walkingDirection, RpcInfo info = default)
    {
        // I am the State Authority here, so I can update the Networked property
        WalkingDirection = walkingDirection;
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority)
        {
            if (Health <= 0)
            {
                Camera.main.transform.SetParent(null);
                Runner.Despawn(Object);
                return;
            }

            if (_outOfCombatDelay.ExpiredOrNotRunning(Runner) && _selfHealDelay.ExpiredOrNotRunning(Runner))
            {
                Health = Math.Min(Health + MaxHealth / 5, MaxHealth);
                _selfHealDelay = TickTimer.CreateFromSeconds(Runner, 2);
            }

            if (KillsUntilLevelUp <= 0)
            {
                Level++;
                KillsUntilLevelUp = 10;
                MaxHealth = 33 * Level;
                Health = MaxHealth;
            }

            // We are the server in here, so we need to update weapon
            _weaponFromServer = TysonsHelpers.DeserializeWeapon(WeaponNetworkStruct);
        }

        if (GetInput(out NetworkInputData data))
        {
            data.direction.Normalize();

            if (HasInputAuthority)
            {
                RPC_SendWalkingDirectionUpdate(data.direction);
            }

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
            if (HasStateAuthority && _attackDelay.ExpiredOrNotRunning(Runner) && data.buttons.IsSet(NetworkInputData.MOUSEBUTTON0))
            {
                // Handle weapon attack
                if (_weaponFromServer != null && _weaponFromServer.FireRate != 0)
                {
                    // Limit shooting rate
                    _attackDelay = TickTimer.CreateFromSeconds(Runner, 1f / _weaponFromServer.FireRate);

                    Runner.Spawn(_prefabAttack, transform.position, Quaternion.identity, Object.InputAuthority,
                    (runner, o) =>
                    {
                        // This callback will be called after instantiating object but before it is synchronized
                        _weaponFromServer.Attack(o);
                        o.GetComponent<AttackProjectile>().ShooterId = PlayerId;
                        o.GetComponent<AttackProjectile>().Direction = (data.clickLocation - (Vector2)transform.position).normalized;
                        o.GetComponent<AttackProjectile>().Color = PlayerColor;
                    });
                }
                else
                {
                    _attackDelay = TickTimer.CreateFromSeconds(Runner, 1);
                }
            }
        }
    }

    public override void Spawned()
    {
        MaxHealth = 33;
        Health = MaxHealth;
        Level = 1;
        KillsUntilLevelUp = 10;

        _animator = GetComponent<Animator>();

        if (Object.HasInputAuthority)
        {
            // When a player first joins we run this code
            FindObjectOfType<Canvas>().transform.Find("Health Bar").gameObject.SetActive(true);
            FindObjectOfType<Canvas>().transform.Find("Loading").gameObject.SetActive(false);
            _inventoryScreen = FindObjectOfType<Canvas>().transform.Find("Inventory Screen").gameObject;
            _healthBarBlood = FindObjectsOfType<Image>(includeInactive: true).First(e => e.gameObject.name == "Health Bar Blood");
            FindObjectsOfType<InventoryItem>(includeInactive: true).ForEach(inventoryItem => Destroy(inventoryItem));
            FindObjectsOfType<InventorySlot>(includeInactive: true).ForEach(inventorySlot => inventorySlot.SetInventoryItem(null));
            FindObjectOfType<Canvas>().transform.Find("Player Level").GetComponent<TextMeshProUGUI>().text = $"{Level}";
            FindObjectOfType<Canvas>().transform.Find("XP Bar").GetComponent<Image>().fillAmount = (10 - KillsUntilLevelUp) / 10f;

            FindObjectsOfType<InventoryItem>(includeInactive: true).ForEach(item => Destroy(item.gameObject));

            // Spawns starting weapon, client-side only
            var sampleWeapon = Instantiate(_inventoryItemPrefab, _inventoryScreen.transform.Find("Items"));
            sampleWeapon.Item = new Weapon() { Name = "Training Sword", Type = "1H Sword", FireRate = 3, MinDamage = 1, MaxDamage = 5, Range = 4, Speed = 10, ShotType = ShotType.STRAIGHT, EquipmentType = EquipmentType.MAIN_HAND };
            sampleWeapon.InventorySlotNumber = FindObjectsOfType<InventorySlot>(includeInactive: true).First(slot => slot.EquipmentType == EquipmentType.MAIN_HAND).SlotNumber;

            RPC_SendColorUpdate(FindObjectsOfType<Image>(includeInactive: true).First(image => image.gameObject.name == "Preview Image").color);

            _inventoryScreen.gameObject.SetActive(false);
        }
    }

    void HealthChanged(NetworkBehaviourBuffer buffer)
    {
        var prevValue = GetPropertyReader<int>(nameof(Health)).Read(buffer);

        if (Health > prevValue)
        {
            GetComponentInChildren<SpriteRenderer>().color = Color.green;
        }
        else
        {
            GetComponentInChildren<SpriteRenderer>().color = Color.red;
        }
    }

    void LevelChanged(NetworkBehaviourBuffer buffer)
    {
        if (Object.HasInputAuthority)
        {
            FindObjectOfType<Canvas>().transform.Find("Player Level").GetComponent<TextMeshProUGUI>().text = $"{Level}";
        }
    }

    void XpChanged(NetworkBehaviourBuffer buffer)
    {
        if (Object.HasInputAuthority)
        {
            FindObjectOfType<Canvas>().transform.Find("XP Bar").GetComponent<Image>().fillAmount = (10 - KillsUntilLevelUp)/10f;
        }
    }

    public override void Render()
    {
        GetComponentInChildren<SpriteRenderer>().color = Color.Lerp(GetComponentInChildren<SpriteRenderer>().color, PlayerColor, Time.deltaTime / 0.4f);
    }

    public void SpawnLootForSelf(int enemyLevel)
    {
        // State Authority gave us permission here so we are okay to spawn loot for ourselves

        InventoryItem? newItem = null;
        
        var random = new System.Random();
        var randomInt = random.Next(0, 18);

        // Minimum armor range will be the armor of two levels below the enemy's level with a minimum of 1
        int minArmor = (int)Math.Floor(_levelToMaxArmor[Math.Max(1, enemyLevel - 2)] / 4f);
        // Maximum armor range will be the armor for the enemy level
        int maxArmor = (int)Math.Floor(_levelToMaxArmor[enemyLevel] / 4f);

        // Minimum DPS range will be the DPS of two levels below the enemy's level with a minimum of 1
        int minDps = _levelToMaxDps[Math.Max(1, enemyLevel - 2)];
        // Maximum DPS range will be the DPS for the enemy level
        int maxDps = _levelToMaxDps[enemyLevel];

        // "Boss" level enemies should always drop loot, for now...
        if (enemyLevel == 10)
        {
            randomInt = random.Next(0, 5);
        }

        switch (randomInt)
        {
            case 0:
                newItem = Instantiate(_inventoryItemPrefab, _inventoryScreen.transform.Find("Items"));
                newItem.Item = Weapon.GenerateWeapon(random.Next(minDps, maxDps + 1));
                SpawnLootForSelf(enemyLevel); // roll again :)
                break;
            case 1:
                if (enemyLevel == 1) { SpawnLootForSelf(enemyLevel); return; } // Level 1 enemies do not spawn armor
                newItem = Instantiate(_inventoryItemPrefab, _inventoryScreen.transform.Find("Items"));
                newItem.Item = new Armor(EquipmentType.HEAD, "Iron Helm", "Head", random.Next(minArmor, maxArmor + 1));
                SpawnLootForSelf(enemyLevel); // roll again :)
                break;
            case 2:
                if (enemyLevel == 1) { SpawnLootForSelf(enemyLevel); return; } // Level 1 enemies do not spawn armor
                newItem = Instantiate(_inventoryItemPrefab, _inventoryScreen.transform.Find("Items"));
                newItem.Item = new Armor(EquipmentType.TORSO, "Iron Chestplate", "Torso", random.Next(minArmor, maxArmor + 1));
                SpawnLootForSelf(enemyLevel); // roll again :)
                break;
            case 3:
                if (enemyLevel == 1) { SpawnLootForSelf(enemyLevel); return; } // Level 1 enemies do not spawn armor
                newItem = Instantiate(_inventoryItemPrefab, _inventoryScreen.transform.Find("Items"));
                newItem.Item = new Armor(EquipmentType.LEGS, "Leather Greaves", "Legs", random.Next(minArmor, maxArmor + 1));
                SpawnLootForSelf(enemyLevel); // roll again :)
                break;
            case 4:
                if (enemyLevel == 1) { SpawnLootForSelf(enemyLevel); return; } // Level 1 enemies do not spawn armor
                newItem = Instantiate(_inventoryItemPrefab, _inventoryScreen.transform.Find("Items"));
                newItem.Item = new Armor(EquipmentType.FEET, "Leather Boots", "Feet", random.Next(minArmor, maxArmor + 1));
                SpawnLootForSelf(enemyLevel); // roll again :)
                break;
            default:
                break;
        }
        
        if (newItem != null)
        {
            newItem.InventorySlotNumber = FindObjectsOfType<InventorySlot>(includeInactive: true).Where(slot => !slot.IsEquipmentSlot && slot.InventoryItem == null).Min(slot => slot.SlotNumber);
        }
    }

    private Dictionary<int, int> _levelToMaxArmor = new Dictionary<int, int>
    {
        {1, 4},
        {2, 13},
        {3, 26},
        {4, 39},
        {5, 52},
        {6, 65},
        {7, 78},
        {8, 91},
        {9, 104},
        {10, 117},
        {11, 130},
        {12, 143},
        {13, 156},
        {14, 169},
        {15, 182},
        {16, 195},
        {17, 208},
        {18, 221},
        {19, 234},
        {20, 247},
        {21, 260},
        {22, 273},
        {23, 286},
        {24, 299},
        {25, 312},
        {26, 325},
        {27, 338},
        {28, 351},
        {29, 364},
        {30, 400}
    };

    private Dictionary<int, int> _levelToMaxDps = new Dictionary<int, int>
    {
        {1, 20},
        {2, 30},
        {3, 40},
        {4, 50},
        {5, 60},
        {6, 70},
        {7, 80},
        {8, 90},
        {9, 100},
        {10, 600},
        {11, 610},
        {12, 620},
        {13, 630},
        {14, 640},
        {15, 650},
        {16, 660},
        {17, 670},
        {18, 680},
        {19, 690},
        {20, 5000},
        {21, 5080},
        {22, 5160},
        {23, 5240},
        {24, 5320},
        {25, 5400},
        {26, 5480},
        {27, 5560},
        {28, 5640},
        {29, 5720},
        {30, 10000}
    };

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out AttackProjectile attackProjectile))
        {
            if (attackProjectile.ShooterId != PlayerId)
            {
                if (HasStateAuthority)
                {
                    int damageAmount = (int)(attackProjectile.Damage * (1f - (Armor / 1000f)));
                    Health -= damageAmount;
                    Debug.Log($"Player with {Armor} armor took {attackProjectile.Damage} reduced to {damageAmount} damage and now has {Health} health!");

                    _outOfCombatDelay = TickTimer.CreateFromSeconds(Runner, 7);

                    Runner.Despawn(attackProjectile.Object);
                }
                else
                {
                    Destroy(attackProjectile.gameObject);
                }
            }
        }
    }

    public void TakeDamageFromSwordSwing(int swordDamage)
    {
        if (HasStateAuthority)
        {
            int damageAmount = (int)(swordDamage * (1f - (Armor / 1000f)));
            Health -= damageAmount;
            Debug.Log($"Player with {Armor} armor took {damageAmount} reduced to {damageAmount} damage and now has {Health} health!");

            _outOfCombatDelay = TickTimer.CreateFromSeconds(Runner, 7);
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
 