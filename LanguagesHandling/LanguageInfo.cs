// <copyright file="LanguageInfo.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Echoglossian.LanguagesHandling;

/// <summary>
///     Represents information about a language.
/// </summary>
public class LanguageInfo
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="LanguageInfo" /> class.
    /// </summary>
    /// <param name="code">Iso LanguageInfo Code.</param>
    /// <param name="languageName">LanguageInfo name.</param>
    /// <param name="fontName">Necessary font name.</param>
    /// <param name="exclusiveCharsToAdd">List of all the needed chars to be added.</param>
    /// <param name="supportedEngines">Supported translation engines.</param>
    public LanguageInfo(
        string code,
        string languageName,
        string fontName,
        string exclusiveCharsToAdd,
        List<int>? supportedEngines)
    {
        this.Code = code ?? string.Empty;
        this.LanguageName = languageName ?? string.Empty;
        this.FontName = fontName ?? string.Empty;
        this.ExclusiveCharsToAdd = exclusiveCharsToAdd ?? string.Empty;
        this.SupportedEngines = supportedEngines ?? null;
    }

    public string Code { get; set; }

    public string LanguageName { get; set; }

    public string FontName { get; set; }

    public string ExclusiveCharsToAdd { get; set; }

    public List<int>? SupportedEngines { get; set; }

    public bool IsEngineSupported(int engineId)
    {
        return this.SupportedEngines?.Contains(engineId) == true;
    }

    public override string ToString()
    {
        return
            $"Code: {this.Code},\n LanguageName: {this.LanguageName},\n FontName: {this.FontName},\n ExclusiveCharsToAdd: {this.ExclusiveCharsToAdd},\n SupportedEngines: {this.SupportedEngines}";
    }
}