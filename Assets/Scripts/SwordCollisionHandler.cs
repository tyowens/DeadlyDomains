using System.Collections.Generic;
using UnityEngine;

public class SwordCollisionHandler : MonoBehaviour
{
    private HashSet<int> _playersHit = new HashSet<int>();

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnTriggerEnter2D(Collider2D collider)
    {
        foreach (var player in collider.GetComponentsInChildren<PlayerMovement>())
        {
            if (!_playersHit.Contains(player.PlayerId))
            {
                _playersHit.Add(player.PlayerId);
                player.TakeDamageFromSwordSwing(10);
                Debug.Log($"Sword hit collider of Player: {player.PlayerId}");
            }
        }
    }

    public void ClearPlayersHit()
    {
        _playersHit.Clear();
    }
}
