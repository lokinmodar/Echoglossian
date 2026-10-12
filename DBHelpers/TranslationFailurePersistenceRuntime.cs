// <copyright file="TranslationFailurePersistenceRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;

namespace Echoglossian.DBHelpers;

/// <summary>Owns the process-lifetime coordinator-backed failure writer.</summary>
internal static class TranslationFailurePersistenceRuntime
{
    private static TranslationFailurePersistenceWriter? writer;

    internal static void Register(TranslationFailurePersistenceWriter registeredWriter)
    {
        writer = registeredWriter;
    }

    internal static void Unregister(TranslationFailurePersistenceWriter registeredWriter)
    {
        if (ReferenceEquals(writer, registeredWriter))
        {
            writer = null;
        }
    }

    internal static void RecordFailure(string sourceText, string sourceLanguage, string targetLanguage, int engine, string reason, string? origin)
    {
        if (!TranslationPersistenceGuard.IsPersistentFailureReason(reason) || string.IsNullOrWhiteSpace(sourceText))
        {
            return;
        }

        // Suppress equivalent provider calls immediately; committed data is published only after SaveChangesAsync.
        TranslationFailureCacheManager.RememberTransientFailure(sourceText, sourceLanguage, targetLanguage, engine, reason, TimeSpan.FromMinutes(5));
        if (writer is null)
        {
            return;
        }

        _ = writer.TryPersist(sourceText, sourceLanguage, targetLanguage, engine, reason, origin, out _);
    }
}
