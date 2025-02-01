using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

public class StaticCallHandler : NetworkBehaviour
{
    [Networked] private NetworkDictionary<int, int> _playerIds => default;

    private BasicSpawner _basicSpawner;

    // Start is called before the first frame update
    void Start()
    {
        _basicSpawner = FindObjectOfType<BasicSpawner>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority )
        {
            _playerIds.ForEach(player =>
            {
                _basicSpawner.RespawnDeadPlayer(player.Key);
                _playerIds.Remove(player.Key);
            });
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
    public void RPC_RequestRespawn(int playerId, RpcInfo info = default)
    {
        // I am the State Authority here, so I can update the Networked property
        if (!_playerIds.ContainsKey(playerId))
        {
            _playerIds.Add(playerId, 0);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, HostMode = RpcHostMode.SourceIsServer)]
    public void RPC_SendLootToPlayer(int playerId, int enemyLevel, RpcInfo info = default)
    {
        // Here we as State Authority give permission to client to spawn loot
        if (playerId == FindObjectOfType<BasicSpawner>().MyPlayerId)
        {
            FindObjectsOfType<PlayerMovement>().First(playerMovement => playerMovement.HasInputAuthority).SpawnLootForSelf(enemyLevel);
        }
    }
}
