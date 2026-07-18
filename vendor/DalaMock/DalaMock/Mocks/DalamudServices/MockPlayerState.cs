namespace DalaMock.Core.Mocks.DalamudServices;

public class MockPlayerState : IPlayerState, IMockService
{
    public int GetAttribute(PlayerAttribute attribute)
    {
        return 0;
    }

    public byte GetGrandCompanyRank(LSheets.GrandCompany grandCompany)
    {
        return 0;
    }

    public short GetClassJobLevel(ClassJob classJob)
    {
        return 0;
    }

    public int GetClassJobExperience(ClassJob classJob)
    {
        return 0;
    }

    public float GetDesynthesisLevel(ClassJob classJob)
    {
        return 0;
    }

    public bool IsLoaded { get; set; }

    public string CharacterName { get; set; }

    public uint EntityId { get; set; }

    public ulong ContentId { get; set; }

    public RowRef<World> CurrentWorld { get; set; }

    public RowRef<World> HomeWorld { get; set; }

    public Sex Sex { get; set; }

    public RowRef<Race> Race { get; set; }

    public RowRef<Tribe> Tribe { get; set; }

    public RowRef<ClassJob> ClassJob { get; set; }

    public short Level { get; set; }

    public bool IsLevelSynced { get; set; }

    public short EffectiveLevel { get; set; }

    public RowRef<GuardianDeity> GuardianDeity { get; set; }

    public byte BirthMonth { get; set; }

    public byte BirthDay { get; set; }

    public RowRef<ClassJob> FirstClass { get; set; }

    public RowRef<Town> StartTown { get; set; }

    public int BaseStrength { get; set; }

    public int BaseDexterity { get; set; }

    public int BaseVitality { get; set; }

    public int BaseIntelligence { get; set; }

    public int BaseMind { get; set; }

    public int BasePiety { get; set; }

    public RowRef<LSheets.GrandCompany> GrandCompany { get; set; }

    public RowRef<Aetheryte> HomeAetheryte { get; set; }

    public IReadOnlyList<RowRef<Aetheryte>> FavoriteAetherytes { get; set; }

    public RowRef<Aetheryte> FreeAetheryte { get; set; }

    public uint BaseRestedExperience { get; set; }

    public short PlayerCommendations { get; set; }

    public byte DeliveryLevel { get; set; }

    public MentorVersion MentorVersion { get; set; }

    public bool IsMentor { get; set; }

    public bool IsBattleMentor { get; set; }

    public bool IsTradeMentor { get; set; }

    public bool IsNovice { get; set; }

    public bool IsReturner { get; set; }

    public string ServiceName => "Player State";
}
