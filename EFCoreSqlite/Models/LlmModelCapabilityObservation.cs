// <copyright file="LlmModelCapabilityObservation.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Represents one provider capability-feedback observation retained for
///     later audit or conservative rule promotion.
/// </summary>
[Table("llmmodelcapabilityobservations")]
public sealed class LlmModelCapabilityObservation
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

    /// <summary>Gets or sets the observed model identifier.</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>Gets or sets the rejected parameter name.</summary>
    public string ParameterName { get; set; } = string.Empty;

    /// <summary>Gets or sets the provider response status code.</summary>
    public int StatusCode { get; set; }

    /// <summary>Gets or sets the provider error code.</summary>
    public string ProviderErrorCode { get; set; } = string.Empty;

    /// <summary>Gets or sets the sanitized provider message excerpt.</summary>
    public string MessageExcerpt { get; set; } = string.Empty;

    /// <summary>Gets or sets when the observation occurred in UTC.</summary>
    public DateTime ObservedAtUtc { get; set; } = DateTime.UtcNow;
}
