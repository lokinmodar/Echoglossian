// <copyright file="IComplexEntity.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models
{
  /// <summary>
  /// Enhances the IMultiTextEntity interface to include additional properties and methods
  /// </summary>
  internal interface IComplexEntity : IMultiTextEntity
  {
    /// <summary>
    /// Gets the type of the StringArrayData.
    /// </summary>
    /// <returns>Returns the type of the StringArrayData.</returns>
    string GetType();

    /// <summary>
    /// Sets the type of the StringArrayData.
    /// </summary>
    /// <param name="type">The type to set.</param>
    void SetType(string type);

    /// <summary>
    /// Gets the raw data of the StringArrayData.
    /// </summary>
    /// <returns>Returns the raw data of the StringArrayData.</returns>
    byte[]? GetRawData();

    /// <summary>
    /// Sets the raw data of the StringArrayData.
    /// </summary>
    /// <param name="rawData">The raw data to set.</param>
    void SetRawData(byte[]? rawData);

    /// <summary>
    /// Gets the formatted raw data of the StringArrayData.
    /// </summary>
    /// <returns>Returns the formatted raw data of the StringArrayData.</returns>
    string? GetFormattedRawData();

    /// <summary>
    /// Sets the formatted raw data of the StringArrayData.
    /// </summary>
    /// <param name="formattedRawData">The formatted raw data to set.</param>
    void SetFormattedRawData(string? formattedRawData);

    /// <summary>
    /// Gets the translated strings with payloads of the StringArrayData.
    /// </summary>
    /// <returns>Returns the translated strings with payloads of the StringArrayData.</returns>
    string? GetTranslatedStringsWithPayloads();

    /// <summary>
    /// Sets the translated strings with payloads of the StringArrayData.
    /// </summary>
    /// <param name="translatedStringsWithPayloads">The translated strings with payloads to set.</param>
    void SetTranslatedStringsWithPayloads(string? translatedStringsWithPayloads);
  }
}
