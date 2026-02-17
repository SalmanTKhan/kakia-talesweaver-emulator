namespace Kakia.TW.Shared.Network
{
	public static partial class PacketTable
	{
		private static void LoadTalesWeaver()
		{
			// Tales Weaver packets are framed with a 0xAA header containing the length.
			// Therefore, from a framing perspective, they are almost all "Dynamic".
			// The OpCode is the first byte of the payload.

			// Bidirectional / Special
			Register(Op.Handshake, 0x00, Dynamic);
			Register(Op.Acknowledge, 0x02, 5);
			Register(Op.Heartbeat, 0x24, Dynamic);

			// Server -> Client responses
			Register(Op.ServerRedirect, 0x03, Dynamic);
			Register(Op.Unknown04Response, 0x04, Dynamic);
			Register(Op.Unknown05Response, 0x05, Dynamic);
			Register(Op.WorldResponse, 0x07, Dynamic);
			Register(Op.StatUpdateResponse, 0x08, Dynamic);
			Register(Op.UserPositionResponse, 0x0B, Dynamic);
			Register(Op.ChatResponse, 0x0D, Dynamic);
			Register(Op.MapChangeResponse, 0x15, Dynamic);
			Register(Op.SkillListResponse, 0x16, Dynamic);
			Register(Op.EnvironmentResponse, 0x1F, Dynamic);
			Register(Op.DialogResponse, 0x17, Dynamic);
			Register(Op.MetaFilesResponse, 0x18, Dynamic);
			Register(Op.LoginSecurityResponse, 0x3C, Dynamic);
			Register(Op.AttackTargetResponse, 0x3F, Dynamic);
			Register(Op.FriendDialogResponse, 0x44, Dynamic);
			Register(Op.AttackResultResponse, 0x48, Dynamic);
			Register(Op.LoginResponse, 0x50, Dynamic);
			Register(Op.ServerListResponse, 0x56, Dynamic);
			Register(Op.CharEffectResponse, 0x5C, Dynamic);
			Register(Op.CharacterSelectListResponse, 0x6B, Dynamic);
			Register(Op.CreateCharacterResponse, 0x7C, Dynamic);
			Register(Op.ConnectedResponse, 0x7E, Dynamic);

			// Server -> Client acknowledgments
			Register(Op.HandshakeAck, 0x68, Dynamic);
			Register(Op.AttackAck, 0x4A, Dynamic);
			Register(Op.LoadCompleteAck, 0x4D, Dynamic);
			Register(Op.EntityClickAck, 0x70, Dynamic);

			// Entity Interaction packets (used during NPC dialog)
			Register(Op.EntityFocusResponse, 0x83, Dynamic);
			Register(Op.InteractionConfirmResponse, 0x14, Dynamic);
			Register(Op.InteractionTimerResponse, 0x49, Dynamic);

			// Client -> Server requests
			Register(Op.StatIncreaseRequest, 0x0A, Dynamic);
			Register(Op.ChatRequest, 0x0E, Dynamic);
			Register(Op.InitConnectRequest, 0x0F, Dynamic);
			Register(Op.ReconnectRequest, 0x10, Dynamic);
			Register(Op.DirectionUpdateRequest, 0x11, Dynamic);
			Register(Op.AttackRequest, 0x13, Dynamic);
			Register(Op.TriggerRequest, 0x1B, Dynamic);
			Register(Op.Unknown21Request, 0x21, Dynamic);
			Register(Op.CheckNameRequest, 0x28, Dynamic);
			Register(Op.CharacterInfoUpdateRequest, 0x2A, Dynamic);
			Register(Op.SelectCharacterRequest, 0x2B, Dynamic);
			Register(Op.CreateCharacterRequest, 0x2C, Dynamic);
			Register(Op.Unknown2ERequest, 0x2E, Dynamic);
			Register(Op.SetPoseRequest, 0x32, Dynamic);
			Register(Op.MovementRequest, 0x33, Dynamic);
			Register(Op.UiActionRequest, 0x37, Dynamic);
			Register(Op.Unknown39Request, 0x39, Dynamic);
			Register(Op.Unknown3DRequest, 0x3D, Dynamic);
			Register(Op.EntityClickRequest, 0x43, Dynamic);
			Register(Op.Unknown45Request, 0x45, Dynamic);
			Register(Op.Unknown51Request, 0x51, Dynamic);
			Register(Op.Unknown55Request, 0x55, Dynamic);
			Register(Op.TargetEntityRequest, 0x59, Dynamic);
			Register(Op.Unknown5FRequest, 0x5F, Dynamic);
			Register(Op.Unknown60Request, 0x60, Dynamic);
			Register(Op.Unknown63Request, 0x63, Dynamic);
			Register(Op.LoginRequest, 0x66, Dynamic);
			Register(Op.ServerSelectRequest, 0x67, Dynamic);
			Register(Op.Unknown6ARequest, 0x6A, Dynamic);
			Register(Op.NpcDialogAnswerRequest, 0x6C, Dynamic);
			Register(Op.Unknown77Request, 0x77, Dynamic);
			Register(Op.AttackStart, 0xB4, Dynamic);
			// Note: DebugSourceLineRequest uses 0x7C which conflicts with CreateCharacterResponse
			// Handler still works - the packet direction determines which handler is called
		}
	}
}
