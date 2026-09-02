// <copyright file="PersistenceWorkKey.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Persistence;

/// <summary>
///     Identifies one canonical persistence work item.
/// </summary>
internal readonly record struct PersistenceWorkKey
{
  /// <summary>
  ///     Initializes a new instance of the <see cref="PersistenceWorkKey" />
  ///     struct.
  /// </summary>
  /// <param name="domain">The domain that owns the canonical identity.</param>
  /// <param name="canonicalIdentity">The stable identity within the domain.</param>
  /// <exception cref="ArgumentException">
  ///     <paramref name="domain" /> or <paramref name="canonicalIdentity" />
  ///     is blank.
  /// </exception>
  internal PersistenceWorkKey(string domain, string canonicalIdentity)
  {
    if (string.IsNullOrWhiteSpace(domain))
    {
      throw new ArgumentException("A persistence work domain is required.", nameof(domain));
    }

    if (string.IsNullOrWhiteSpace(canonicalIdentity))
    {
      throw new ArgumentException(
          "A canonical persistence identity is required.",
          nameof(canonicalIdentity));
    }

    this.Domain = domain;
    this.CanonicalIdentity = canonicalIdentity;
  }

  /// <summary>
  ///     Gets the domain that owns the canonical identity.
  /// </summary>
  internal string Domain { get; }

  /// <summary>
  ///     Gets the stable identity within the domain.
  /// </summary>
  internal string CanonicalIdentity { get; }
}
