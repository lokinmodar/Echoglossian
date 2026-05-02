// <copyright file="StringArrayDatas.partial.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Echoglossian.EFCoreSqlite.Models
{
  public partial class StringArrayDatas : IComplexEntity
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="StringArrayDatas"/> class.
    /// </summary>
    public StringArrayDatas()
    {
    }

    /// <inheritdoc />
    public new string GetType()
    {
      return this.Type ?? string.Empty;
    }

    /// <inheritdoc />
    public void SetType(string type)
    {
      this.Type = type;
    }

    /// <inheritdoc />
    public int GetSize()
    {
      return this.Size;
    }

    /// <inheritdoc />
    public void SetSize(int size)
    {
      this.Size = size;
    }

    /// <inheritdoc />
    public byte[]? GetRawData()
    {
      return this.RawData;
    }

    /// <inheritdoc />
    public void SetRawData(byte[]? rawData)
    {
      this.RawData = rawData;
    }

    /// <inheritdoc />
    public string? GetFormattedRawData()
    {
      return this.FormattedRawData;
    }

    /// <inheritdoc />
    public void SetFormattedRawData(string? formattedRawData)
    {
      this.FormattedRawData = formattedRawData;
    }

    /// <inheritdoc />
    public string? GetTranslatedStringsWithPayloads()
    {
      return this.TranslatedStringsWithPayloads;
    }

    /// <inheritdoc />
    public void SetTranslatedStringsWithPayloads(string? translatedStringsWithPayloads)
    {
      this.TranslatedStringsWithPayloads = translatedStringsWithPayloads;
    }

    /// <inheritdoc />
    public string GetOriginalLang()
    {
      return this.OriginalLang ?? string.Empty;
    }

    /// <inheritdoc />
    public void SetOriginalLang(string lang)
    {
      this.OriginalLang = lang;
    }

    /// <inheritdoc />
    public string? GetOriginalStrings()
    {
      return this.OriginalStrings;
    }
    /// <inheritdoc />
    public void SetOriginalStrings(string? originalStrings)
    {
      this.OriginalStrings = originalStrings;
    }

    /// <inheritdoc />
    public string? GetTranslationLang()
    {
      return this.TranslationLang;
    }

    /// <inheritdoc />
    public void SetTranslationLang(string? translationLang)
    {
      this.TranslationLang = translationLang;
    }
    /// <inheritdoc />
    public string? GetTranslatedStrings()
    {
      return this.TranslatedStrings;
    }

    /// <inheritdoc />
    public void SetTranslatedStrings(string? translatedStrings)
    {
      this.TranslatedStrings = translatedStrings;
    }

    /// <inheritdoc />
    public int? GetTranslationEngine()
    {
      return this.TranslationEngine;
    }

    /// <inheritdoc />
    public void SetTranslationEngine(int engine)
    {
      this.TranslationEngine = engine;
    }

    /// <inheritdoc />
    public string? GetGameVersion()
    {
      return this.GameVersion;
    }

    /// <inheritdoc />
    public void SetGameVersion(string? gameVersion)
    {
      this.GameVersion = gameVersion;
    }

    /// <inheritdoc />
    public DateTime GetCreatedAt()
    {
      return this.CreatedAt;
    }

    /// <inheritdoc />
    public void SetCreatedAt(DateTime createdAt)
    {
      this.CreatedAt = createdAt;
    }

    /// <inheritdoc />
    public DateTime GetUpdatedAt()
    {
      return this.UpdatedAt;
    }

    /// <inheritdoc />
    public void SetUpdatedAt(DateTime updatedAt)
    {
      this.UpdatedAt = updatedAt;
    }

    /// <inheritdoc />
    public string GetEntityKey()
    {
      return this.Id.ToString();
    }

    /// <inheritdoc />
    public void SetEntityKey(string key)
    {
      if (int.TryParse(key, out int id))
      {
        this.Id = id;
      }
      else
      {
        throw new ArgumentException("Invalid key format for StringArrayDatas entity.");
      }
    }

    /// <inheritdoc />
    public byte[]? GetRTLLangTranslationImageData()
    {
      return null; // StringArrayDatas does not use RTL language translation image data.
    }

    /// <inheritdoc />
    public void SetRTLLangTranslationImageData(byte[]? rtlLangTranslationImageData)
    {
      // StringArrayDatas does not use RTL language translation image data, so this method is intentionally left empty.
    }

    /// <inheritdoc />
    public string? GetOriginalSecondaryText()
    {
      return this.TranslatedStringsWithPayloads;
    }

    /// <inheritdoc />
    public void SetOriginalSecondaryText(string? originalSecondaryText)
    {
      this.TranslatedStringsWithPayloads = originalSecondaryText;
    }

    /// <inheritdoc />
    public string? GetTranslatedSecondaryText()
    {
      return this.TranslatedStrings;
    }

    /// <inheritdoc />
    public void SetTranslatedSecondaryText(string? translatedSecondaryText)
    {
      this.TranslatedStrings = translatedSecondaryText;
    }

    /// <inheritdoc />
    public string GetOriginalText()
    {
      return this.OriginalStrings ?? string.Empty;
    }

    /// <inheritdoc />
    public void SetOriginalText(string? originalText)
    {
      this.OriginalStrings = originalText;
    }

    /// <inheritdoc />
    public string? GetTranslatedText()
    {
      return this.TranslatedStrings;
    }

    /// <inheritdoc />
    public void SetTranslatedText(string? translatedText)
    {
      this.TranslatedStrings = translatedText;
    }

    /// <inheritdoc />
    public string? GetOriginalLangCode()
    {
      return this.OriginalLang;
    }

    /// <inheritdoc />
    public void SetOriginalLangCode(string? originalLangCode)
    {
      this.OriginalLang = originalLangCode;
    }


  }
}
