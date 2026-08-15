// <copyright file="LlmModelCapabilityRule.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.Capabilities;

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Represents one persisted LLM capability overlay rule.
/// </summary>
[Table("llmmodelcapabilityrules")]
public sealed class LlmModelCapabilityRule
{
    /// <summary>Gets or sets the primary key.</summary>
    [Key]
    public long Id { get; set; }

    /// <summary>Gets or sets the engine identifier.</summary>
    public string Engine { get; set; } = string.Empty;

    /// <summary>Gets or sets the provider scope.</summary>
    public string ProviderScope { get; set; } = string.Empty;

    /// <summary>Gets or sets the endpoint scope.</summary>
    public string EndpointScope { get; set; } = string.Empty;

    /// <summary>Gets or sets the model matching strategy.</summary>
    public string MatchType { get; set; } = string.Empty;

    /// <summary>Gets or sets the model identifier or family prefix.</summary>
    public string MatchValue { get; set; } = string.Empty;

    /// <summary>Gets or sets the governed parameter name.</summary>
    public string ParameterName { get; set; } = string.Empty;

    /// <summary>Gets or sets the parameter support state.</summary>
    public string SupportState { get; set; } = string.Empty;

    /// <summary>Gets or sets the inclusive minimum supported value.</summary>
    public float? MinValue { get; set; }

    /// <summary>Gets or sets the inclusive maximum supported value.</summary>
    public float? MaxValue { get; set; }

    /// <summary>Gets or sets serialized allowed enum values.</summary>
    public string AllowedEnumValuesJson { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether default-only values are omitted.</summary>
    public bool OmitWhenDefaultOnly { get; set; }

    /// <summary>Gets or sets the rule source.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Gets or sets the rule rationale.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets the source confidence.</summary>
    public string Confidence { get; set; } = string.Empty;

    /// <summary>Gets or sets when the rule was observed in UTC.</summary>
    public DateTime ObservedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets when the rule expires in UTC.</summary>
    public DateTime? ExpiresAtUtc { get; set; }

    /// <summary>Gets or sets when the row was created in UTC.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets when the row was last updated in UTC.</summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Creates a persisted exact-model capability rule.
    /// </summary>
    /// <param name="engine">The engine identifier.</param>
    /// <param name="providerScope">The provider identity.</param>
    /// <param name="endpointScope">The endpoint identity.</param>
    /// <param name="modelId">The complete model identifier.</param>
    /// <param name="parameterName">The governed parameter.</param>
    /// <param name="supportState">The known support state.</param>
    /// <param name="minValue">The inclusive lower bound, when known.</param>
    /// <param name="maxValue">The inclusive upper bound, when known.</param>
    /// <param name="omitWhenDefaultOnly"><see langword="true" /> to omit the parameter when only its default is allowed; otherwise, <see langword="false" />.</param>
    /// <param name="source">The source that established the rule.</param>
    /// <param name="reason">The explanation for the rule.</param>
    /// <returns>A persisted exact-model capability rule.</returns>
    public static LlmModelCapabilityRule CreateExactModel(
        string engine,
        string providerScope,
        string endpointScope,
        string modelId,
        LlmCapabilityParameterName parameterName,
        LlmCapabilitySupportState supportState,
        float? minValue = null,
        float? maxValue = null,
        bool omitWhenDefaultOnly = false,
        string source = "",
        string reason = "")
    {
        return new LlmModelCapabilityRule
        {
            Engine = engine,
            ProviderScope = providerScope,
            EndpointScope = endpointScope,
            MatchType = LlmCapabilityRuleMatchType.ExactModel.ToString(),
            MatchValue = modelId,
            ParameterName = parameterName.ToString(),
            SupportState = supportState.ToString(),
            MinValue = minValue,
            MaxValue = maxValue,
            OmitWhenDefaultOnly = omitWhenDefaultOnly,
            Source = source,
            Reason = reason,
        };
    }

    /// <summary>
    ///     Converts this persisted row to the shared resolver rule contract.
    /// </summary>
    /// <returns>The shared capability rule definition.</returns>
    public LlmCapabilityRuleDefinition ToDefinition()
    {
        return new LlmCapabilityRuleDefinition(
            this.Engine,
            this.ProviderScope,
            this.EndpointScope,
            Enum.TryParse<LlmCapabilityRuleMatchType>(this.MatchType, out var matchType)
                ? matchType
                : (LlmCapabilityRuleMatchType)(-1),
            this.MatchValue,
            Enum.TryParse<LlmCapabilityParameterName>(this.ParameterName, out var parameterName)
                ? parameterName
                : (LlmCapabilityParameterName)(-1),
            Enum.TryParse<LlmCapabilitySupportState>(this.SupportState, out var supportState)
                ? supportState
                : LlmCapabilitySupportState.Unknown,
            this.MinValue,
            this.MaxValue,
            this.OmitWhenDefaultOnly,
            this.Source,
            this.Reason);
    }
}
