namespace DalaMock.Core.Mocks.DalamudServices;

public class MockUnlockState : IUnlockState, IMockService
{
    /// <inheritdoc/>
    public string ServiceName => "Unlock State";

    /// <inheritdoc/>
    public event IUnlockState.UnlockDelegate? Unlock;

    public void InvokeUnlock(RowRef rowRef)
    {
        this.Unlock?.Invoke(rowRef);
    }

    /// <inheritdoc/>
    public bool IsAchievementListLoaded { get; set; }

    /// <inheritdoc/>
    public bool IsTitleListLoaded { get; set; }

    public HashSet<uint> AchievementComplete = new();
    public HashSet<uint> ActionUnlocked = new();
    public HashSet<uint> AdventureComplete = new();
    public HashSet<uint> AetherCurrentUnlocked = new();
    public HashSet<uint> AetherCurrentCompFlgSetUnlocked = new();
    public HashSet<uint> AozActionUnlocked = new();
    public HashSet<uint> BannerBgUnlocked = new();
    public HashSet<uint> BannerConditionUnlocked = new();
    public HashSet<uint> BannerDecorationUnlocked = new();
    public HashSet<uint> BannerFacialUnlocked = new();
    public HashSet<uint> BannerFrameUnlocked = new();
    public HashSet<uint> BannerTimelineUnlocked = new();
    public HashSet<uint> BuddyActionUnlocked = new();
    public HashSet<uint> BuddyEquipUnlocked = new();
    public HashSet<uint> CharaMakeCustomizeUnlocked = new();
    public HashSet<uint> ChocoboTaxiStandUnlocked = new();
    public HashSet<uint> ClassJobUnlocked = new();
    public HashSet<uint> CompanionUnlocked = new();
    public HashSet<uint> CraftActionUnlocked = new();
    public HashSet<uint> CSBonusContentTypeUnlocked = new();
    public HashSet<uint> EmoteUnlocked = new();
    public HashSet<uint> EmjVoiceNpcUnlocked = new();
    public HashSet<uint> EmjCostumeUnlocked = new();
    public HashSet<uint> GeneralActionUnlocked = new();
    public HashSet<uint> GlassesUnlocked = new();
    public HashSet<uint> GlassesStyleUnlocked = new();
    public HashSet<uint> HowToUnlocked = new();
    public HashSet<uint> InstanceContentUnlocked = new();
    public HashSet<uint> ItemUnlocked = new();
    public HashSet<uint> LeveCompleted = new();
    public HashSet<uint> McGuffinUnlocked = new();
    public HashSet<uint> MJILandmarkUnlocked = new();
    public HashSet<uint> MKDLoreUnlocked = new();
    public HashSet<uint> MountUnlocked = new();
    public HashSet<uint> NotebookDivisionUnlocked = new();
    public HashSet<uint> OrchestrionUnlocked = new();
    public HashSet<uint> OrnamentUnlocked = new();
    public HashSet<uint> PerformUnlocked = new();
    public HashSet<uint> PublicContentUnlocked = new();
    public HashSet<uint> QuestCompleted = new();
    public HashSet<uint> RecipeUnlocked = new();
    public HashSet<uint> SecretRecipeBookUnlocked = new();
    public HashSet<uint> TitleUnlocked = new();
    public HashSet<uint> TraitUnlocked = new();
    public HashSet<uint> TripleTriadCardUnlocked = new();

    public HashSet<uint> UnlockLinks = new();
    public Dictionary<uint, byte> UnlockLinkProgression = new();

    /// <inheritdoc/>
    public bool IsAchievementComplete(Achievement row)
        => Check(this.AchievementComplete, row.RowId);

    /// <inheritdoc/>
    public bool IsActionUnlocked(LuminaAction row)
        => Check(this.ActionUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsAdventureComplete(Adventure row)
        => Check(this.AdventureComplete, row.RowId);

    /// <inheritdoc/>
    public bool IsAetherCurrentCompFlgSetUnlocked(AetherCurrentCompFlgSet row)
        => Check(this.AetherCurrentCompFlgSetUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsAetherCurrentUnlocked(AetherCurrent row)
        => Check(this.AetherCurrentUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsAozActionUnlocked(AozAction row)
        => Check(this.AozActionUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsBannerBgUnlocked(BannerBg row)
        => Check(this.BannerBgUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsBannerConditionUnlocked(BannerCondition row)
        => Check(this.BannerConditionUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsBannerDecorationUnlocked(BannerDecoration row)
        => Check(this.BannerDecorationUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsBannerFacialUnlocked(BannerFacial row)
        => Check(this.BannerFacialUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsBannerFrameUnlocked(BannerFrame row)
        => Check(this.BannerFrameUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsBannerTimelineUnlocked(BannerTimeline row)
        => Check(this.BannerTimelineUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsBuddyActionUnlocked(BuddyAction row)
        => Check(this.BuddyActionUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsBuddyEquipUnlocked(BuddyEquip row)
        => Check(this.BuddyEquipUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsCharaMakeCustomizeUnlocked(CharaMakeCustomize row)
        => Check(this.CharaMakeCustomizeUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsChocoboTaxiStandUnlocked(ChocoboTaxiStand row)
        => Check(this.ChocoboTaxiStandUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsClassJobUnlocked(ClassJob row)
        => Check(this.ClassJobUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsCompanionUnlocked(Companion row)
        => Check(this.CompanionUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsCraftActionUnlocked(CraftAction row)
        => Check(this.CraftActionUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsCSBonusContentTypeUnlocked(CSBonusContentType row)
        => Check(this.CSBonusContentTypeUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsEmoteUnlocked(Emote row)
        => Check(this.EmoteUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsEmjVoiceNpcUnlocked(EmjVoiceNpc row)
        => Check(this.EmjVoiceNpcUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsEmjCostumeUnlocked(EmjCostume row)
        => Check(this.EmjCostumeUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsGeneralActionUnlocked(GeneralAction row)
        => Check(this.GeneralActionUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsGlassesUnlocked(Glasses row)
        => Check(this.GlassesUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsGlassesStyleUnlocked(GlassesStyle row)
        => Check(this.GlassesStyleUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsHowToUnlocked(HowTo row)
        => Check(this.HowToUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsInstanceContentUnlocked(InstanceContent row)
        => Check(this.InstanceContentUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsItemUnlocked(Item row)
        => Check(this.ItemUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsLeveCompleted(Leve row)
        => Check(this.LeveCompleted, row.RowId);

    /// <inheritdoc/>
    public bool IsMcGuffinUnlocked(McGuffin row)
        => Check(this.McGuffinUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsMJILandmarkUnlocked(MJILandmark row)
        => Check(this.MJILandmarkUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsMKDLoreUnlocked(MKDLore row)
        => Check(this.MKDLoreUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsMountUnlocked(Mount row)
        => Check(this.MountUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsNotebookDivisionUnlocked(NotebookDivision row)
        => Check(this.NotebookDivisionUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsOrchestrionUnlocked(Orchestrion row)
        => Check(this.OrchestrionUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsOrnamentUnlocked(Ornament row)
        => Check(this.OrnamentUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsPerformUnlocked(Perform row)
        => Check(this.PerformUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsPublicContentUnlocked(PublicContent row)
        => Check(this.PublicContentUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsQuestCompleted(Quest row)
        => Check(this.QuestCompleted, row.RowId);

    /// <inheritdoc/>
    public bool IsRecipeUnlocked(Recipe row)
        => Check(this.RecipeUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsSecretRecipeBookUnlocked(SecretRecipeBook row)
        => Check(this.SecretRecipeBookUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsTitleUnlocked(Title row)
        => Check(this.TitleUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsTraitUnlocked(Trait row)
        => Check(this.TraitUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsTripleTriadCardUnlocked(TripleTriadCard row)
        => Check(this.TripleTriadCardUnlocked, row.RowId);

    /// <inheritdoc/>
    public bool IsUnlockLinkUnlocked(uint unlockLink)
        =>
            this.UnlockLinks.Contains(unlockLink);

    /// <inheritdoc/>
    public bool IsUnlockLinkUnlocked(uint unlockLink, byte minimumQuestSequence)
    {
        return this.UnlockLinks.Contains(unlockLink) || (this.UnlockLinkProgression.ContainsKey(unlockLink) &&
                                                         this.UnlockLinkProgression[unlockLink] >= minimumQuestSequence);
    }

    /// <inheritdoc/>
    public bool IsUnlockLinkUnlocked(ushort unlockLink)
        =>
            this.UnlockLinks.Contains(unlockLink);

    /// <inheritdoc/>
    public bool IsItemUnlockable(Item row)
    {
        if (row.ItemAction.RowId == 0)
        {
            return false;
        }

        return (ItemActionAction)row.ItemAction.Value.Action.RowId
               is ItemActionAction.Companion
               or ItemActionAction.BuddyEquip
               or ItemActionAction.Mount
               or ItemActionAction.SecretRecipeBook
               or ItemActionAction.UnlockLink
               or ItemActionAction.TripleTriadCard
               or ItemActionAction.FolkloreTome
               or ItemActionAction.OrchestrionRoll
               or ItemActionAction.FramersKit
               or ItemActionAction.Ornament
               or ItemActionAction.Glasses
               or ItemActionAction.OccultRecords
               or ItemActionAction.SoulShards;
    }

    /// <inheritdoc/>
    public bool IsRowRefUnlocked<T>(RowRef<T> rowRef)
        where T : struct, IExcelRow<T>
    {
        return this.IsRowRefUnlocked((RowRef)rowRef);
    }

    /// <inheritdoc/>
    public bool IsRowRefUnlocked(RowRef rowRef)
    {
        if (rowRef.IsUntyped)
        {
            return false;
        }

        if (rowRef.TryGetValue<Achievement>(out var achievementRow))
        {
            return this.IsAchievementComplete(achievementRow);
        }

        if (rowRef.TryGetValue<LuminaAction>(out var actionRow))
        {
            return this.IsActionUnlocked(actionRow);
        }

        if (rowRef.TryGetValue<Adventure>(out var adventureRow))
        {
            return this.IsAdventureComplete(adventureRow);
        }

        if (rowRef.TryGetValue<AetherCurrent>(out var aetherCurrentRow))
        {
            return this.IsAetherCurrentUnlocked(aetherCurrentRow);
        }

        if (rowRef.TryGetValue<AetherCurrentCompFlgSet>(out var aetherCurrentCompFlgSetRow))
        {
            return this.IsAetherCurrentCompFlgSetUnlocked(aetherCurrentCompFlgSetRow);
        }

        if (rowRef.TryGetValue<AozAction>(out var aozActionRow))
        {
            return this.IsAozActionUnlocked(aozActionRow);
        }

        if (rowRef.TryGetValue<BannerBg>(out var bannerBgRow))
        {
            return this.IsBannerBgUnlocked(bannerBgRow);
        }

        if (rowRef.TryGetValue<BannerCondition>(out var bannerConditionRow))
        {
            return this.IsBannerConditionUnlocked(bannerConditionRow);
        }

        if (rowRef.TryGetValue<BannerDecoration>(out var bannerDecorationRow))
        {
            return this.IsBannerDecorationUnlocked(bannerDecorationRow);
        }

        if (rowRef.TryGetValue<BannerFacial>(out var bannerFacialRow))
        {
            return this.IsBannerFacialUnlocked(bannerFacialRow);
        }

        if (rowRef.TryGetValue<BannerFrame>(out var bannerFrameRow))
        {
            return this.IsBannerFrameUnlocked(bannerFrameRow);
        }

        if (rowRef.TryGetValue<BannerTimeline>(out var bannerTimelineRow))
        {
            return this.IsBannerTimelineUnlocked(bannerTimelineRow);
        }

        if (rowRef.TryGetValue<BuddyAction>(out var buddyActionRow))
        {
            return this.IsBuddyActionUnlocked(buddyActionRow);
        }

        if (rowRef.TryGetValue<BuddyEquip>(out var buddyEquipRow))
        {
            return this.IsBuddyEquipUnlocked(buddyEquipRow);
        }

        if (rowRef.TryGetValue<CSBonusContentType>(out var csBonusContentTypeRow))
        {
            return this.IsCSBonusContentTypeUnlocked(csBonusContentTypeRow);
        }

        if (rowRef.TryGetValue<CharaMakeCustomize>(out var charaMakeCustomizeRow))
        {
            return this.IsCharaMakeCustomizeUnlocked(charaMakeCustomizeRow);
        }

        if (rowRef.TryGetValue<ChocoboTaxiStand>(out var chocoboTaxiStandRow))
        {
            return this.IsChocoboTaxiStandUnlocked(chocoboTaxiStandRow);
        }

        if (rowRef.TryGetValue<ClassJob>(out var classJobRow))
        {
            return this.IsClassJobUnlocked(classJobRow);
        }

        if (rowRef.TryGetValue<Companion>(out var companionRow))
        {
            return this.IsCompanionUnlocked(companionRow);
        }

        if (rowRef.TryGetValue<CraftAction>(out var craftActionRow))
        {
            return this.IsCraftActionUnlocked(craftActionRow);
        }

        if (rowRef.TryGetValue<Emote>(out var emoteRow))
        {
            return this.IsEmoteUnlocked(emoteRow);
        }

        if (rowRef.TryGetValue<GeneralAction>(out var generalActionRow))
        {
            return this.IsGeneralActionUnlocked(generalActionRow);
        }

        if (rowRef.TryGetValue<Glasses>(out var glassesRow))
        {
            return this.IsGlassesUnlocked(glassesRow);
        }

        if (rowRef.TryGetValue<HowTo>(out var howToRow))
        {
            return this.IsHowToUnlocked(howToRow);
        }

        if (rowRef.TryGetValue<InstanceContent>(out var instanceContentRow))
        {
            return this.IsInstanceContentUnlocked(instanceContentRow);
        }

        if (rowRef.TryGetValue<Item>(out var itemRow))
        {
            return this.IsItemUnlocked(itemRow);
        }

        if (rowRef.TryGetValue<Leve>(out var leveRow))
        {
            return this.IsLeveCompleted(leveRow);
        }

        if (rowRef.TryGetValue<MJILandmark>(out var mjiLandmarkRow))
        {
            return this.IsMJILandmarkUnlocked(mjiLandmarkRow);
        }

        if (rowRef.TryGetValue<MKDLore>(out var mkdLoreRow))
        {
            return this.IsMKDLoreUnlocked(mkdLoreRow);
        }

        if (rowRef.TryGetValue<McGuffin>(out var mcGuffinRow))
        {
            return this.IsMcGuffinUnlocked(mcGuffinRow);
        }

        if (rowRef.TryGetValue<Mount>(out var mountRow))
        {
            return this.IsMountUnlocked(mountRow);
        }

        if (rowRef.TryGetValue<NotebookDivision>(out var notebookDivisionRow))
        {
            return this.IsNotebookDivisionUnlocked(notebookDivisionRow);
        }

        if (rowRef.TryGetValue<Orchestrion>(out var orchestrionRow))
        {
            return this.IsOrchestrionUnlocked(orchestrionRow);
        }

        if (rowRef.TryGetValue<Ornament>(out var ornamentRow))
        {
            return this.IsOrnamentUnlocked(ornamentRow);
        }

        if (rowRef.TryGetValue<Perform>(out var performRow))
        {
            return this.IsPerformUnlocked(performRow);
        }

        if (rowRef.TryGetValue<PublicContent>(out var publicContentRow))
        {
            return this.IsPublicContentUnlocked(publicContentRow);
        }

        if (rowRef.TryGetValue<Quest>(out var questRow))
        {
            return this.IsQuestCompleted(questRow);
        }

        if (rowRef.TryGetValue<Recipe>(out var recipeRow))
        {
            return this.IsRecipeUnlocked(recipeRow);
        }

        if (rowRef.TryGetValue<SecretRecipeBook>(out var secretRecipeBookRow))
        {
            return this.IsSecretRecipeBookUnlocked(secretRecipeBookRow);
        }

        if (rowRef.TryGetValue<Title>(out var titleRow))
        {
            return this.IsTitleUnlocked(titleRow);
        }

        if (rowRef.TryGetValue<Trait>(out var traitRow))
        {
            return this.IsTraitUnlocked(traitRow);
        }

        if (rowRef.TryGetValue<TripleTriadCard>(out var tripleTriadCardRow))
        {
            return this.IsTripleTriadCardUnlocked(tripleTriadCardRow);
        }

        return false;
    }

    private static bool Check(HashSet<uint> set, uint rowId)
        => set.Contains(rowId);

    private static uint Id<T>(T row)
        where T : struct, IExcelRow<T>
        => row.RowId;
}
