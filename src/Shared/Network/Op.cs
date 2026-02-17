namespace Kakia.TW.Shared.Network
{
	/// <summary>
	/// List of packet operations (Opcodes).
	/// Naming convention:
	/// - Client -> Server: PascalCase + "Request" suffix
	/// - Server -> Client: PascalCase + "Response" suffix
	/// - Acknowledgments: PascalCase + "Ack" suffix
	/// - Bidirectional/Special: PascalCase (no suffix)
	/// </summary>
	public enum Op : byte
	{
		// ================================================================
		// Bidirectional / Special packets (no suffix)
		// ================================================================
		Handshake = 0x00,
		Acknowledge = 0x02,
		Heartbeat = 0x24,

		// ================================================================
		// Client -> Server requests
		// ================================================================
		StatIncreaseRequest = 0x0A,
		ChatRequest = 0x0E,
		InitConnectRequest = 0x0F,
		ReconnectRequest = 0x10,
		DirectionUpdateRequest = 0x11,
		AttackRequest = 0x13,
		TriggerRequest = 0x1B,
		Unknown21Request = 0x21,
		CheckNameRequest = 0x28,
		CharacterInfoUpdateRequest = 0x2A,
		SelectCharacterRequest = 0x2B,
		CreateCharacterRequest = 0x2C,
		Unknown2ERequest = 0x2E,
		SetPoseRequest = 0x32,
		MovementRequest = 0x33,
		UiActionRequest = 0x37,
		Unknown39Request = 0x39,
		Unknown3DRequest = 0x3D,
		SecurityCodeRequest = 0x18,
		EntityClickRequest = 0x43,
		Unknown45Request = 0x45,
		Unknown51Request = 0x51,
		Unknown55Request = 0x55,
		TargetEntityRequest = 0x59,
		Unknown5FRequest = 0x5F,
		Unknown60Request = 0x60,
		Unknown63Request = 0x63,
		LoginRequest = 0x66,
		ServerSelectRequest = 0x67,
		Unknown6ARequest = 0x6A,
		NpcDialogAnswerRequest = 0x6C,
		Unknown77Request = 0x77,
		DebugSourceLineRequest = 0x7C,
		AttackStart = 0xB4,

		// ================================================================
		// Server -> Client responses
		// ================================================================
		ServerRedirect = 0x03,
		Unknown04Response = 0x04,
		Unknown05Response = 0x05,
		WorldResponse = 0x07,
		StatUpdateResponse = 0x08,
		UserPositionResponse = 0x0B,
		ChatResponse = 0x0D,
		MapChangeResponse = 0x15,
		SkillListResponse = 0x16,
		EnvironmentResponse = 0x1F,
		DialogResponse = 0x17,
		MetaFilesResponse = 0x18,
		LoginSecurityResponse = 0x3C,
		AttackTargetResponse = 0x3F,
		FriendDialogResponse = 0x44,
		AttackResultResponse = 0x48,
		LoginResponse = 0x50,
		ServerListResponse = 0x56,
		CharEffectResponse = 0x5C,
		CharacterSelectListResponse = 0x6B,
		CreateCharacterResponse = 0x7C,
		ConnectedResponse = 0x7E,

		// ================================================================
		// Server -> Client acknowledgments
		// ================================================================
		HandshakeAck = 0x68,
		AttackAck = 0x4A,
		LoadCompleteAck = 0x4D,
		EntityClickAck = 0x70,

		// ================================================================
		// Entity Interaction packets (used during NPC dialog)
		// ================================================================
		EntityFocusResponse = 0x83,
		InteractionConfirmResponse = 0x14,
		InteractionTimerResponse = 0x49,

		Unknown = 0xFF,
	}

	// ----------------------------------------------------------------
	// Sub-Identifiers (Packet Logic Enums)
	// ----------------------------------------------------------------

	/// <summary>
	/// Sub-codes for SecurityCodeRequest (0x18) packets sent by the client.
	/// </summary>
	public enum ClientResponseCode : byte
	{
		Unknown = 0x00,
		WithMessage = 0x01,
		Unknown02 = 0x02,
		Unknown03 = 0x03,
		Unknown04 = 0x04,
		LoadingDone = 0x05
	}

	public enum CharLoginSecurityType : byte
	{
		CodeUpdated = 0x00,
		None = 0x01,
		SetupSecurityCode = 0x02,
		RequestSecurityCode = 0x03
	}

	/// <summary>
	/// Represents sub-actions for the WorldResponse (0x07) packet.
	/// </summary>
	public enum WorldPacketId : byte
	{
		Spawn = 0,
		Despawn = 1,
		Died = 2,
	}

	public enum SpawnType : byte
	{
		Player = 0x00,
		Npc = 0x01,
		MonsterNpc = 0x02,
		Item = 0x03,
		Portal = 0x04,
		Reactor = 0x05,
		Pet = 0x06,
		MonsterNpc2 = 0x07,
		SummonedCreature = 0x08,
		MonsterNpc3 = 0x09
	}

	/// <summary>
	/// Represents sub-actions for the CharacterAction packet.
	/// </summary>
	public enum CharacterActionType : byte
	{
		UpdateCharacter = 0,
		AddItem = 1,
		AddItemAlias = 2,
		BankOperation = 3,
		AddRuneBankItem = 4,
	}

	/// <summary>
	/// Represents sub-actions for the RemoveAction packet.
	/// </summary>
	public enum RemoveActionType : byte
	{
		RemoveEntrustedItem = 0,
		RemoveEntity = 1,
		RemoveEntityAlias = 2,
		RemoveBankItem = 3,
		RemoveRuneBankItem = 4,
	}

	/// <summary>
	/// Represents sub-actions for the QuestInfo packet.
	/// </summary>
	public enum QuestActionType : byte
	{
		QuestList = 0,
		UpdateQuest = 1,
	}

	/// <summary>
	/// Represents sub-actions for the MapPing packet.
	/// </summary>
	public enum MapPingActionType : byte
	{
		SetPing = 0,
		ClearPing = 1,
	}

	/// <summary>
	/// Represents sub-actions for the ObjectInteraction packet.
	/// </summary>
	public enum ObjectInteractionType : byte
	{
		PartyAction = 0,
		SkillEffect = 1,
		StatusEffect = 2,
		AttachGameObject = 3,
		CreateObjectGrid = 4,
		SetTimer = 5,
		GuildAction = 8,
	}

	/// <summary>
	/// Represents sub-actions for the OpenMarket packet.
	/// </summary>
	public enum MarketActionType : byte
	{
		MarketStart = 0x11,
		MarketList = 0x12,
		Bid = 0x21,
		BidResponse = 0x22,
		Purchase = 0x23,
		PurchaseResponse = 0x24,
		RegisterItem = 0x31,
		RegisterItemResponse = 0x32,
		ClaimItem = 0x41,
		ClaimItemResponse = 0x42,
		CancelSale = 0x43,
		CancelSaleResponse = 0x44,
		CloseMarket = 0x82,
		RefreshMarket = 0x84,
		MarketNotification = 0x91,
		MarketNotificationResponse = 0x92,
		ReceiveItemList = 0x99,
	}

	/// <summary>
	/// Represents sub-actions for the InventoryAction packet.
	/// </summary>
	public enum InventoryActionType : byte
	{
		CharacterStateChange = 0,
		AddItem = 1,
		RemoveItem = 2,
		UpdateItem = 3,
		CharacterInteraction = 10,
		CharacterInteractionResponse = 11,
	}

	/// <summary>
	/// Represents sub-actions for the FriendDialogResponse (0x44) packet.
	/// </summary>
	public enum FriendDialogActionType : byte
	{
		FriendMemoAction1 = 0,
		FriendMemoAction2 = 1,
		EventScriptBroadcast = 2,
		FriendMemoAction3 = 4,
		NpcDialog = 5,
		DialogResponse = 6,
		BossBattleResult = 7,
	}

	public enum FriendDialogFlagType : byte
	{
		HasPortrait = 2,
	}

	public enum DialogActionType : byte
	{
		DialogSelectMenu = 4,
		Dialog = 5,
	}

	public enum DialogOptionType : byte
	{
		NumberInput1 = 0,
		NumberInput2 = 1,
		HasOptions = 2,
	}
}
