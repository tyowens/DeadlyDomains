#nullable enable
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
    [Networked] private TickTimer _shootingDelay { get; set; }
    [Networked] private TickTimer _standingDelay { get; set; }

    [SerializeField] private GameObject _damageIndicator;
    [SerializeField] private int _aggroRadius;
    [SerializeField] private AttackProjectile _prefabAttack;
    [SerializeField] private int _playerWithClaim = -1;

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
                FindObjectOfType<StaticCallHandler>().RPC_SendLootToPlayer(_playerWithClaim);
            }

            _spawner.OnEnemyDeath();

            Runner.Despawn(Object);

            return;
        }

        var allPlayers = FindObjectsOfType<PlayerMovement>();
        _closestPlayer = allPlayers.OrderBy(player => ((Vector2)player.transform.position - (Vector2)transform.position).magnitude).FirstOrDefault();

        if (_closestPlayer != null)
        {
            if (((Vector2)_closestPlayer.transform.position - (Vector2)transform.position).magnitude <= _aggroRadius)
            {
                NavMeshPath path = new();
                NavMesh.CalculatePath(transform.position, _closestPlayer.transform.position, NavMesh.AllAreas, path);
                var direction = ((Vector2)path.corners[1] - (Vector2)transform.position).normalized;
                gameObject.GetComponent<Rigidbody2D>().MovePosition((Vector2)transform.position + (direction * 3f * Runner.DeltaTime));

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
                if (_wanderingPoint == null)
                {
                    _wanderingPoint = _spawner.GetPointInSpawnRadius();
                }

                if (!_standingDelay.ExpiredOrNotRunning(Runner))
                {
                    // We are standing, so let's get a next spot
                    _wanderingPoint = _spawner.GetPointInSpawnRadius(); // this could be optimized to not get called repeatedly while standing still
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
                        var direction = ((Vector2)path.corners[1] - (Vector2)transform.position).normalized;
                        gameObject.GetComponent<Rigidbody2D>().MovePosition((Vector2)transform.position + (direction * 3f * Runner.DeltaTime));
                    }
                }
            }

            gameObject.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
        }
    }
  
    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        _closestPlayer = null;
        _random = new System.Random();

        _enemyWeapon = new Weapon
        {
            MinDamage = 10,
            MaxDamage = 20,
            FireRate = 2f,
            Speed = 10f,
            Range = 10f,
            ShotType =  ShotType.STRAIGHT
        };

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

        var targetColor = new Color(195/255f, 49/255f, 72/255f);
        GetComponentInChildren<SpriteRenderer>().color = Color.Lerp(GetComponentInChildren<SpriteRenderer>().color, targetColor, Time.deltaTime / 0.4f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out AttackProjectile attackProjectile) && attackProjectile.ShooterId != -1)
        {
            if (HasStateAuthority)
            {
                _health -= attackProjectile.Damage;
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

        // Calculate health
        if (_level < 10)
        {
            _maxHealth = 50 * _level;
        }
        else if (_level < 20)
        {
            _maxHealth = 100 * _level;
        }
        else if (_level < 30)
        {
            _maxHealth = 450 * _level;
        }
        else
        {
            _maxHealth = 9999; // oopsie :)
        }

        _health = _maxHealth;
        gameObject.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.gameObject.name == "Level").GetComponent<TextMeshPro>().text = $"{_level}";
    }

    public void SetSpawner(EnemySpawner spawner)
    {
        _spawner = spawner;
    }
}
