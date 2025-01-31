using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    public const byte MOUSEBUTTON0 = 1;

    public Vector3 direction;
    public Vector2 clickLocation;
    public NetworkButtons buttons;
}
