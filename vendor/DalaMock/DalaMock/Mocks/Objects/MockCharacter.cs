namespace DalaMock.Core.Mocks.Objects;

public class MockCharacter : ICharacter
{
    private readonly MockClientState clientState;
    private SeString name = SeString.Empty;
    private ulong gameObjectId;
    private uint entityId;
    private uint dataId;
    private uint ownerId;
    private uint baseId;
    private ushort objectIndex;
    private ObjectKind objectKind;
    private byte subKind;
    private byte yalmDistanceX;
    private byte yalmDistanceZ;
    private bool isDead;
    private bool isTargetable;
    private Vector3 position;
    private float rotation;
    private float hitboxRadius;
    private ulong targetObjectId;
    private IGameObject? targetObject;
    private IntPtr address;
    private uint currentHp;
    private uint maxHp;
    private uint currentMp;
    private uint maxMp;
    private uint currentGp;
    private uint maxGp;
    private uint currentCp;
    private uint maxCp;
    private byte shieldPercentage;
    private RowRef<ClassJob> classJob;
    private byte level;
    private byte[] customize;
    private SeString companyTag;
    private uint nameId;
    private RowRef<OnlineStatus> onlineStatus;
    private StatusFlags statusFlags;
    private RowRef<Mount>? currentMount;
    private RowRef<Companion>? currentMinion;

    public MockCharacter(MockClientState clientState)
    {
        this.clientState = clientState;
    }

    /// <inheritdoc/>
    public bool Equals(IGameObject? other)
    {
        return this.GameObjectId == other?.GameObjectId;
    }

    /// <inheritdoc/>
    public bool IsValid()
    {
        return this.clientState.LocalContentId == 0;
    }

    [ImGuiGroup("Basic")]
    public SeString Name
    {
        get => this.name;
        set => this.name = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Ids")]
    public ulong GameObjectId
    {
        get => this.gameObjectId;
        set => this.gameObjectId = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Ids")]
    public uint EntityId
    {
        get => this.entityId;
        set => this.entityId = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Ids")]
    public uint DataId
    {
        get => this.dataId;
        set => this.dataId = value;
    }

    [ImGuiGroup("Ids")]
    public uint BaseId
    {
        get => this.baseId;
        set => this.baseId = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Ids")]
    public uint OwnerId
    {
        get => this.ownerId;
        set => this.ownerId = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Misc")]
    public ushort ObjectIndex
    {
        get => this.objectIndex;
        set => this.objectIndex = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Basic")]
    public ObjectKind ObjectKind
    {
        get => this.objectKind;
        set => this.objectKind = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Basic")]
    public byte SubKind
    {
        get => this.subKind;
        set => this.subKind = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Position")]
    public byte YalmDistanceX
    {
        get => this.yalmDistanceX;
        set => this.yalmDistanceX = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Position")]
    public byte YalmDistanceZ
    {
        get => this.yalmDistanceZ;
        set => this.yalmDistanceZ = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Basic")]
    public bool IsDead
    {
        get => this.isDead;
        set => this.isDead = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Basic")]
    public bool IsTargetable
    {
        get => this.isTargetable;
        set => this.isTargetable = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Position")]
    public Vector3 Position
    {
        get => this.position;
        set => this.position = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Position")]
    public float Rotation
    {
        get => this.rotation;
        set => this.rotation = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Position")]
    public float HitboxRadius
    {
        get => this.hitboxRadius;
        set => this.hitboxRadius = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Ids")]
    public ulong TargetObjectId
    {
        get => this.targetObjectId;
        set => this.targetObjectId = value;
    }

    /// <inheritdoc/>
    public IGameObject? TargetObject
    {
        get => this.targetObject;
        set => this.targetObject = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Misc")]
    public IntPtr Address
    {
        get => this.address;
        set => this.address = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Basic")]
    public uint CurrentHp
    {
        get => this.currentHp;
        set => this.currentHp = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Basic")]
    public uint MaxHp
    {
        get => this.maxHp;
        set => this.maxHp = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Basic")]
    public uint CurrentMp
    {
        get => this.currentMp;
        set => this.currentMp = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Basic")]
    public uint MaxMp
    {
        get => this.maxMp;
        set => this.maxMp = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Basic")]
    public uint CurrentGp
    {
        get => this.currentGp;
        set => this.currentGp = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Basic")]
    public uint MaxGp
    {
        get => this.maxGp;
        set => this.maxGp = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Basic")]
    public uint CurrentCp
    {
        get => this.currentCp;
        set => this.currentCp = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Basic")]
    public uint MaxCp
    {
        get => this.maxCp;
        set => this.maxCp = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Basic")]
    public byte ShieldPercentage
    {
        get => this.shieldPercentage;
        set => this.shieldPercentage = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Basic")]
    public RowRef<ClassJob> ClassJob
    {
        get => this.classJob;
        set => this.classJob = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Basic")]
    public byte Level
    {
        get => this.level;
        set => this.level = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Basic")]
    public Span<byte> Customize
    {
        get => this.customize;
        set => this.customize = value.ToArray();
    }

    public ICustomizeData CustomizeData { get; set; }

    /// <inheritdoc/>
    [ImGuiGroup("Basic")]
    public SeString CompanyTag
    {
        get => this.companyTag;
        set => this.companyTag = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Ids")]
    public uint NameId
    {
        get => this.nameId;
        set => this.nameId = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Basic")]
    public RowRef<OnlineStatus> OnlineStatus
    {
        get => this.onlineStatus;
        set => this.onlineStatus = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Basic")]
    public StatusFlags StatusFlags
    {
        get => this.statusFlags;
        set => this.statusFlags = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Basic")]
    public RowRef<Mount>? CurrentMount
    {
        get => this.currentMount;
        set => this.currentMount = value;
    }

    /// <inheritdoc/>
    [ImGuiGroup("Basic")]
    public RowRef<Companion>? CurrentMinion
    {
        get => this.currentMinion;
        set => this.currentMinion = value;
    }
}
