using Fusion;
using UnityEngine;

public class AttackProjectile : NetworkBehaviour
{
    // Shooter ID of -1 indicates enemy projectile
    [Networked] public int ShooterId { get; set; }
    [Networked] public Vector2 Direction { get; set; }
    [Networked] public int Damage { get; set; }
    [Networked] public float Range { get; set; }
    [Networked] public float Speed { get; set; }
    [Networked, OnChangedRender(nameof(OnColorChanged))] public Color Color { get; set; }

    public override void FixedUpdateNetwork()
    {
        transform.position += (Vector3)(Direction * Speed * Runner.DeltaTime);

        Range -= Speed * Runner.DeltaTime;
        if (Range <= 0)
        {
            // Despawn internally checks for State Authority
            Runner.Despawn(Object);
            return;
        }
    }

    public void Init()
    {
    }

    public override void Spawned()
    {
        gameObject.GetComponentInChildren<SpriteRenderer>().color = Color;
    }

    public void OnColorChanged()
    {
        gameObject.GetComponentInChildren<SpriteRenderer>().color = Color;
    }
}
