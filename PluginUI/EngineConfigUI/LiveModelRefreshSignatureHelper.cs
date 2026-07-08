// <copyright file="LiveModelRefreshSignatureHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Security.Cryptography;
using System.Text;

namespace Echoglossian.PluginUI.EngineConfigUI;

/// <summary>
///     Represents one input component for a live model-refresh signature.
/// </summary>
/// <param name="Name">The stable component name.</param>
/// <param name="Value">The current component value.</param>
/// <param name="Sensitive">
///     Whether the component should be reduced to a short stable hash instead
///     of storing the raw value in memory.
/// </param>
internal readonly record struct LiveModelRefreshSignatureComponent(
    string Name,
    string? Value,
    bool Sensitive = false);

/// <summary>
///     Builds stable live model-refresh signatures without embedding raw
///     secret material such as API keys.
/// </summary>
internal static class LiveModelRefreshSignatureHelper
{
    /// <summary>
    ///     Builds one stable refresh signature from the provided components.
    /// </summary>
    /// <param name="components">The ordered signature components.</param>
    /// <returns>The stable signature string.</returns>
    public static string Build(params LiveModelRefreshSignatureComponent[] components)
    {
        if (components.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        foreach (LiveModelRefreshSignatureComponent component in components)
        {
            if (builder.Length > 0)
            {
                builder.Append('|');
            }

            builder.Append(component.Name);
            builder.Append('=');
            builder.Append(
                component.Sensitive
                    ? ComputeStableHash(component.Value)
                    : Normalize(component.Value));
        }

        return builder.ToString();
    }

    private static string Normalize(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string ComputeStableHash(string? value)
    {
        string normalizedValue = Normalize(value);
        if (normalizedValue.Length == 0)
        {
            return string.Empty;
        }

        byte[] inputBytes = Encoding.UTF8.GetBytes(normalizedValue);
        byte[] hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToHexString(hashBytes, 0, 8).ToLowerInvariant();
    }
}
