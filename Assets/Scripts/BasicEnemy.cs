#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

public class BasicEnemy : NetworkBehaviour
{
    [Networked] private int _level { get; set; }
    [Networked] private int _health { get; set; }
    [Networked] private int _maxHealth { get; set; }
    [Networked] private int _playerWithClaim { get; set; }
    [Networked] private TickTimer _shootingDelay { get; set; }
    [Networked] private TickTimer _standingDelay { get; set; }
    [Networked] private TickTimer _timeUntilGiveUp { get; set; }
    [Networked]
    [OnChangedRender(nameof(IsRetreatingChanged))]
    private bool _isRetreating { get; set; }

    [SerializeField] private GameObject _damageIndicator;
    [SerializeField] private int _aggroRadius;
    [SerializeField] private AttackProjectile _prefabAttack;

    private ChangeDetector _changeDetector;
    private PlayerMovement? _closestPlayer;
    private Weapon _enemyWeapon;
    private EnemySpawner _spawner;
    private Vector2? _wanderingPoint;
    private System.Random _random;

    public override void FixedUpdateNetwork()
    {
        if (_health <= 0)
        {
            if (HasStateAuthority && _playerWithClaim != -1)
            {
                PlayerMovement playerToReward = FindObjectsOfType<PlayerMovement>().FirstOrDefault(player => player.PlayerId == _playerWithClaim);
                if (playerToReward != null && playerToReward.Level <= _level)
                {
                    FindObjectsOfType<PlayerMovement>().First(player => player.PlayerId == _playerWithClaim).KillsUntilLevelUp -= 1;
                }
                
                FindObjectOfType<StaticCallHandler>().RPC_SendLootToPlayer(_playerWithClaim, _level);
            }

            _spawner.OnEnemyDeath();

            Runner.Despawn(Object);

            return;
        }

        var allPlayers = FindObjectsOfType<PlayerMovement>();
        _closestPlayer = allPlayers.OrderBy(player => ((Vector2)player.transform.position - (Vector2)transform.position).magnitude).FirstOrDefault();

        if (HasStateAuthority && _timeUntilGiveUp.ExpiredOrNotRunning(Runner))
        {
            _playerWithClaim = -1;
        }

        if (_closestPlayer != null)
        {
            // We have not been hit in so long and are outside of range of our spawner
            _isRetreating = _isRetreating || _timeUntilGiveUp.ExpiredOrNotRunning(Runner) && ((Vector2)_spawner.transform.position - (Vector2)transform.position).magnitude > _spawner.spawnRadius * 1.5f;
            Vector2 directionToMove = Vector2.zero;
            if (((Vector2)_closestPlayer.transform.position - (Vector2)transform.position).magnitude <= _aggroRadius
                    && !_isRetreating)
            {
                NavMeshPath path = new();
                NavMesh.CalculatePath(transform.position, _closestPlayer.transform.position, NavMesh.AllAreas, path);
                directionToMove = ((Vector2)path.corners[1] - (Vector2)transform.position).normalized;

                if (HasStateAuthority && _shootingDelay.ExpiredOrNotRunning(Runner))
                {
                    // Limit shooting rate
                    _shootingDelay = TickTimer.CreateFromSeconds(Runner, 1f);

                    Runner.Spawn(_prefabAttack, transform.position, Quaternion.identity, Object.InputAuthority,
                    (runner, o) =>
                    {
                        // This callback will be called after instantiating object but before it is synchronized
                        _enemyWeapon.Attack(o);
                        o.GetComponent<AttackProjectile>().ShooterId = -1;
                        o.GetComponent<AttackProjectile>().Direction = ((Vector2)_closestPlayer.transform.position - (Vector2)transform.position).normalized;
                        o.GetComponent<AttackProjectile>().Color = Color.red;
                    });
                }
            }
            else
            {
                if (HasStateAuthority)
                {
                    if (_isRetreating)
                    {
                        // We have returned to our range
                        if (((Vector2)_spawner.transform.position - (Vector2)transform.position).magnitude < _spawner.spawnRadius)
                        {
                            _isRetreating = false;
                        }
                    }

                    if (_wanderingPoint == null)
                    {
                        _wanderingPoint = _spawner.GetPointInSpawnRadius();
                    }

                    if (!_standingDelay.ExpiredOrNotRunning(Runner))
                    {
                        // We are standing, so let's get a next spot
                        _wanderingPoint = _spawner.GetPointInSpawnRadius(); //TODO this could be optimized to not get called repeatedly while standing still
                    }
                    else
                    {
                        if (((Vector2)transform.position - (Vector2)_wanderingPoint!).magnitude < 0.1)
                        {
                            // Reached our wandering destination, so let's wait for a bit
                            _standingDelay = TickTimer.CreateFromSeconds(Runner, _random.Next(2, 7));
                        }
                        else
                        {
                            // Move towards the wandering point
                            NavMeshPath path = new NavMeshPath();
                            NavMesh.CalculatePath(transform.position, (Vector2)_wanderingPoint, NavMesh.AllAreas, path);
                            directionToMove = ((Vector2)path.corners[1] - (Vector2)transform.position).normalized;
                        }
                    }
                }
            }

            var closeEnemies = FindObjectsOfType<BasicEnemy>()
                                .Where(enemy => ((Vector2)enemy.transform.position - (Vector2)transform.position).magnitude < 2)
                                .Where(enemy => ((Vector2)enemy.transform.position - (Vector2)transform.position).magnitude > 0.2)
                                .ToList();
            
            foreach (var closeEnemy in closeEnemies)
            {
                directionToMove += ((Vector2)closeEnemy.transform.position - (Vector2)transform.position).normalized * -0.3f * (2f - (((Vector2)closeEnemy.transform.position - (Vector2)transform.position).magnitude) / 2f);
            }

            gameObject.GetComponent<Rigidbody2D>().MovePosition((Vector2)transform.position + (directionToMove.normalized * 3f * Runner.DeltaTime));
        }

        gameObject.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
    }
  
    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        _closestPlayer = null;
        _random = new System.Random();
        
        if (HasStateAuthority)
        {
            _playerWithClaim = -1;
        }

        // When client player joins late, need to update UI elementa to match current state
        if (_maxHealth > 0)
        {
            GetComponentInChildren<SpriteRenderer>().color = Color.red;
            var healthBarTransform = gameObject.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.gameObject.name == "Health Bar");
            var scale = healthBarTransform.localScale;
            healthBarTransform.localScale = new Vector3((float)_health/_maxHealth, scale.y, scale.z);
            gameObject.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.gameObject.name == "Level").GetComponent<TextMeshPro>().text = $"{_level}";
        }
    }

    private Dictionary<int, int> _levelToDps = new Dictionary<int, int>
    {
        {1, 4},
        {2, 8},
        {3, 13},
        {4, 17},
        {5, 22},
        {6, 26},
        {7, 31},
        {8, 36},
        {9, 42},
        {10, 47},
        {11, 53},
        {12, 60},
        {13, 67},
        {14, 73},
        {15, 80},
        {16, 86},
        {17, 94},
        {18, 101},
        {19, 109},
        {20, 113},
        {21, 123},
        {22, 134},
        {23, 144},
        {24, 154},
        {25, 166},
        {26, 178},
        {27, 189},
        {28, 202},
        {29, 215},
        {30, 300}
    };

    public override void Render()
    {
        if (_changeDetector != null)
        {
            foreach (var change in _changeDetector.DetectChanges(this))
            {
                switch (change)
                {
                    case nameof(_health):
                        GetComponentInChildren<SpriteRenderer>().color = Color.red;
                        var healthBarTransform = gameObject.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.gameObject.name == "Health Bar");

                        if (healthBarTransform == null || _maxHealth <= 0) { break; }

                        var scale = healthBarTransform.localScale;
                        healthBarTransform.localScale = new Vector3((float)_health/_maxHealth, scale.y, scale.z);
                        break;
                    case nameof(_level):
                        gameObject.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.gameObject.name == "Level").GetComponent<TextMeshPro>().text = $"{_level}";
                        break;
                }
            }
        }

        var targetColor = (_playerWithClaim == -1 || _playerWithClaim == FindObjectOfType<BasicSpawner>().MyPlayerId) ? new Color(195/255f, 49/255f, 72/255f) : Color.grey;
        GetComponentInChildren<SpriteRenderer>().color = Color.Lerp(GetComponentInChildren<SpriteRenderer>().color, targetColor, Time.deltaTime / 0.4f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out AttackProjectile attackProjectile)
                && attackProjectile.ShooterId != -1
                && !_isRetreating)
        {
            if (HasStateAuthority)
            {
                _health -= attackProjectile.Damage;
                _timeUntilGiveUp = TickTimer.CreateFromSeconds(Runner, 3);
                //Debug.Log($"Enemy took {attackProjectile.Damage} damage from Player: {attackProjectile.ShooterId} and now has {_health} health!");

                if (_playerWithClaim == -1)
                {
                    _playerWithClaim = attackProjectile.ShooterId;
                }
            }

            var wasDamageDoneByMe = FindObjectsOfType<PlayerMovement>().First(e => e.HasInputAuthority).PlayerId == attackProjectile.ShooterId;
            if (wasDamageDoneByMe)
            {
                var damageIndicator = Instantiate(_damageIndicator, FindObjectOfType<Canvas>().transform);
                damageIndicator.GetComponent<TextMeshProUGUI>().text = $"{attackProjectile.Damage}";
                damageIndicator.GetComponent<DamageIndicator>().SetWorldPosition(transform.position);
            }

            if (HasStateAuthority)
            {
                Runner.Despawn(attackProjectile.Object);
            }
        }
    }
    
    public void CalculateMaxHealth(int level)
    {
        if (!HasStateAuthority) { return; }

        _level = level;

        _enemyWeapon = new Weapon
        {
            MinDamage = (int)Math.Ceiling(_levelToDps[_level]/2f),
            MaxDamage = (int)Math.Ceiling(_levelToDps[_level]/2f),
            FireRate = 2f,
            Speed = 10f,
            Range = 10f,
            ShotType =  ShotType.STRAIGHT
        };

        // Wait a bit before attacking to prevent spawn sniping
        _shootingDelay = TickTimer.CreateFromSeconds(Runner, 2f);

        // Calculate health
        if (_level < 10)
        {
            _maxHealth = 50 * _level;
        }
        else if (_level < 20)
        {
            _maxHealth = 150 * _level;
        }
        else if (_level < 30)
        {
            _maxHealth = 1000 * _level;
        }
        else
        {
            _maxHealth = 345000; // Boss
        }

        _health = _maxHealth;
        gameObject.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.gameObject.name == "Level").GetComponent<TextMeshPro>().text = $"{_level}";
    }

    public void SetSpawner(EnemySpawner spawner)
    {
        _spawner = spawner;
    }

    void IsRetreatingChanged(NetworkBehaviourBuffer buffer)
    {
        SpriteRenderer healthSprite = gameObject.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.gameObject.name == "Health Bar Sprite").GetComponent<SpriteRenderer>();
        if (_isRetreating)
        {
            healthSprite.color = Color.grey;
            _health = _maxHealth;
        }
        else
        {
            healthSprite.color = new Color(52/255f, 168/255f, 66/255f);
        }
    }
}
