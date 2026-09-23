using Fusion;

public struct TetrisInput : INetworkInput
{
    public NetworkBool LeftHeld;
    public NetworkBool RightHeld;
    public NetworkBool UpPressed;
    public NetworkBool DownHeld;
    public NetworkBool SpacePressed;

    public NetworkBool HoldPressed; // NEW: The 'Hold' key (C or Shift)

    public NetworkBool Skill1Pressed;
    public NetworkBool Skill2Pressed;
    public NetworkBool Skill3Pressed;
}