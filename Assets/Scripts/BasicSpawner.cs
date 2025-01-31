using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Fusion.Sockets;
using System;
using UnityEngine.SceneManagement;
using System.Linq;
using UnityEngine.EventSystems;
using Fusion.Addons.Physics;
using UnityEngine.UI;
using TMPro;

public class BasicSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    public int MyPlayerId => _myPlayerId;
    private NetworkRunner _networkRunner;
    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();
    /// <summary>
    /// Temporary bool to store value from Update() since Update() is called more frequently,
    /// this allows us to catch quick taps
    /// </summary>
    private bool _mouseButton0;
    private bool _isDragging;
    private bool _tooltipShown;
    private Vector2 _mousePosition;
    private GameObject _respawnButton;
    private int _myPlayerId = -1;

    [SerializeField] private NetworkPrefabRef _playerPrefab;
    [SerializeField] private WeaponTooltip _weaponTooltipPrefab;
    [SerializeField] private ArmorTooltip _armorTooltipPrefab;
    [SerializeField] private NetworkPrefabRef _respawnHandlerPrefab;

#region INetworkRunnerCallbacks
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { OnPlayerJoinedInternal(runner, player); }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { OnPlayerLeftInternal(runner, player); }
    public void OnInput(NetworkRunner runner, NetworkInput input) { OnInputInternal(runner, input); }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player){ }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player){ }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data){ }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress){ }
#endregion

    // Start is called before the first frame update
    void Start()
    {
        _respawnButton = FindObjectsOfType<Button>(includeInactive: true).First(button => button.gameObject.name == "Respawn Button").gameObject;
    }

    // Update is called once per frame
    void Update()
    {
        // If we are not in the game yet, skip this logic below
        if (_networkRunner == null) { return; }

        if (_myPlayerId <= 0 && FindObjectsOfType<PlayerMovement>().Any())
        {
            _myPlayerId = FindObjectsOfType<PlayerMovement>().FirstOrDefault(entry => entry.HasInputAuthority).PlayerId;
            FindObjectsOfType<TextMeshProUGUI>(includeInactive: true).First(button => button.gameObject.name == "Player ID - DEBUG").text = $"{_myPlayerId}";
        }

        _mouseButton0 = _mouseButton0 | Input.GetMouseButton(0);

        if (Input.GetMouseButton(0))
        {
            _mousePosition = Input.mousePosition;
        }

        // If we do not have a valid player under our control, show the respawn button
        if (FindObjectsOfType<PlayerMovement>().Any())
        {
            if (!FindObjectsOfType<PlayerMovement>().Any(player => player.HasInputAuthority))
            {
                _respawnButton.SetActive(true);
            }
            else
            {
                _respawnButton.SetActive(false);
            }
        }
    }

    public void StartGame_OnClick()
    {
        FindObjectOfType<Canvas>().transform.Find("Title Screen").gameObject.SetActive(false);
        FindObjectOfType<Canvas>().transform.Find("Loading").gameObject.SetActive(true);
        StartGame(GameMode.Host);
    }

    public void JoinGame_OnClick()
    {
        FindObjectOfType<Canvas>().transform.Find("Title Screen").gameObject.SetActive(false);
        FindObjectOfType<Canvas>().transform.Find("Loading").gameObject.SetActive(true);
        StartGame(GameMode.Client);
    }

    private async void StartGame(GameMode gameMode)
    {
        // Create the Fusion runner and let it know that we will be providing user input
        _networkRunner = gameObject.AddComponent<NetworkRunner>();
        _networkRunner.ProvideInput = true;

        var runnerSimPhysics = gameObject.AddComponent<RunnerSimulatePhysics2D>();
        runnerSimPhysics.ClientPhysicsSimulation = ClientPhysicsSimulation.SimulateAlways;

        // Create the NetworkSceneInfo from the current scene
        SceneRef scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex); // Fusion thing
        NetworkSceneInfo sceneInfo = new NetworkSceneInfo(); // Fusion thing
        if (scene.IsValid)
        {
            sceneInfo.AddSceneRef(scene, LoadSceneMode.Additive); // Additive loads the scene specified without closing all others
        }

        bool startGameSuccess = false;
        int retryAttempt = 1;
        // Start or join (depends on gamemode) a session with a specific name
        while (!startGameSuccess && retryAttempt < 11)
        {
            var startGameResult = await _networkRunner.StartGame(new StartGameArgs()
            {
                GameMode = gameMode,
                SessionName = "TestRoom", //TODO change this
                Scene = scene,
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
            });

            startGameSuccess = startGameResult.Ok;
            retryAttempt++;
        }
    }

    private void OnPlayerJoinedInternal(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            Vector3 spawnPosition = new Vector3();
            NetworkObject playerNetworkObject = runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);
            playerNetworkObject.GetComponent<PlayerMovement>().PlayerId = player.PlayerId;
            _spawnedCharacters.Add(player, playerNetworkObject);

            if (!FindObjectsOfType<StaticCallHandler>().Any())
            {
                runner.Spawn(_respawnHandlerPrefab, new Vector3(), Quaternion.identity);
            }
        }
    }

    private void OnPlayerLeftInternal(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
        {
            runner.Despawn(networkObject);
            _spawnedCharacters.Remove(player);
        }
    }

    private void OnInputInternal(NetworkRunner runner, NetworkInput input)
    {
        NetworkInputData data = new();
        
        if (Input.GetKey(KeyCode.W))
            data.direction += Vector3.up;

        if (Input.GetKey(KeyCode.S))
            data.direction += Vector3.down;

        if (Input.GetKey(KeyCode.A))
            data.direction += Vector3.left;

        if (Input.GetKey(KeyCode.D))
            data.direction += Vector3.right;

        if (!IsPointerOverUIElement(GetEventSystemRaycastResults()))
        {
            data.buttons.Set(NetworkInputData.MOUSEBUTTON0, _mouseButton0);
            if (_mouseButton0)
            {
                data.clickLocation = (Vector2)Camera.main.ScreenToWorldPoint(_mousePosition);
            }
        }
        else
        {
            if (_mouseButton0)
            {
                if (!_isDragging && GetEventSystemRaycastResults().Any(e => e.gameObject.GetComponent<InventoryItem>()))
                {
                    _isDragging = true;
                    GetEventSystemRaycastResults().First(e => e.gameObject.GetComponent<InventoryItem>()).gameObject.GetComponent<InventoryItem>().IsBeingDragged = true;
                }
            }
            else
            {
                var inventoryScreen = FindObjectOfType<Canvas>().transform.Find("Inventory Screen");

                var raycastResult = GetEventSystemRaycastResults().Where(raycastResult => raycastResult.gameObject.TryGetComponent<InventoryItem>(out _)).FirstOrNull();
                bool shouldDeleteTooltips = false;

                if (raycastResult != null)
                {
                    var item = ((RaycastResult)raycastResult).gameObject.GetComponent<InventoryItem>().Item;

                    if (item != null)
                    {
                        if (!_tooltipShown)
                        {
                            if (item is Weapon weapon)
                            {
                                var weaponTooltip = Instantiate(_weaponTooltipPrefab, inventoryScreen);
                                weaponTooltip.GetComponent<WeaponTooltip>().Weapon = weapon;
                            }
                            else if (item is Armor armor)
                            {
                                var armorTooltip = Instantiate(_armorTooltipPrefab, inventoryScreen);
                                armorTooltip.GetComponent<ArmorTooltip>().Armor = armor;
                            }
                            _tooltipShown = true;
                        }
                    }
                    else
                    {
                        shouldDeleteTooltips = true;
                    }
                }
                else
                {
                    shouldDeleteTooltips = true;
                }

                if (shouldDeleteTooltips)
                {
                    FindObjectsOfType<WeaponTooltip>().ForEach(tooltip => Destroy(tooltip.gameObject));
                    FindObjectsOfType<ArmorTooltip>().ForEach(tooltip => Destroy(tooltip.gameObject));
                    _tooltipShown = false;
                }

                foreach (var inventoryItem in GetEventSystemRaycastResults().Where(e => e.gameObject.GetComponent<InventoryItem>()))
                {
                    inventoryItem.gameObject.GetComponent<InventoryItem>().IsBeingDragged = false;
                    _isDragging = false;
                }
            }
        }
        
        _mouseButton0 = Input.GetMouseButton(0);

        input.Set(data);
    }

    private bool IsPointerOverUIElement(List<RaycastResult> eventSystemRaysastResults)
    {
        return eventSystemRaysastResults.Any(raycastResult => raycastResult.gameObject.layer == LayerMask.NameToLayer("UI") && !raycastResult.gameObject.CompareTag("raycastIgnore"));
    }

    public static List<RaycastResult> GetEventSystemRaycastResults()
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = Input.mousePosition;
        List<RaycastResult> raysastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, raysastResults);
        return raysastResults;
    }

    public void RespawnButton_OnClick()
    {
        FindObjectOfType<StaticCallHandler>().RPC_RequestRespawn(_myPlayerId);
    }

    public void RespawnDeadPlayer(int playerId)
    {
        if (!_networkRunner.IsServer)
        {
            // Oh no! We are trying to respawn a dead player as a client :o
            return;
        }

        // I am the State Authority here, so I can spawn a new player GameObject
        var existingPlayerEntryKey = _spawnedCharacters.First(player => player.Key.PlayerId == playerId).Key;

        Vector3 spawnPosition = new Vector3();
        NetworkObject playerNetworkObject = _networkRunner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, existingPlayerEntryKey);
        playerNetworkObject.GetComponent<PlayerMovement>().PlayerId = playerId;
        _spawnedCharacters[existingPlayerEntryKey] = playerNetworkObject;
    }
}
