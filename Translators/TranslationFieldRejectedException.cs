// <copyright file="TranslationFieldRejectedException.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators;

/// <summary>
///     Represents an expected rejection of one named translation field after
///     provider result validation.
/// </summary>
public sealed class TranslationFieldRejectedException : InvalidOperationException
{
  /// <summary>
  ///     Initializes a new instance of the
  ///     <see cref="TranslationFieldRejectedException" /> class.
  /// </summary>
  /// <param name="fieldName">The stable rejected field name.</param>
  /// <param name="failureReason">The stable validation failure reason.</param>
  public TranslationFieldRejectedException(string fieldName, string failureReason)
      : base($"Translation field '{fieldName}' was rejected: {failureReason}")
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
    ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);
    this.FieldName = fieldName;
    this.FailureReason = failureReason;
  }

  /// <summary>
  ///     Gets the stable name of the rejected field.
  /// </summary>
  public string FieldName { get; }

  /// <summary>
  ///     Gets the stable reason that prevented accepting the field.
  /// </summary>
  public string FailureReason { get; }
}
