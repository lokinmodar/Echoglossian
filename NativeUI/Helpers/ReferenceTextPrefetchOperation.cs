// <copyright file="ReferenceTextPrefetchOperation.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.DBHelpers;
using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Persistence;
using Echoglossian.Translators;
using Echoglossian.Translators.Helpers;
using Newtonsoft.Json;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Owns one captured prefetch operation while the shared coordinator and
///     translation broker perform its work. The Framework only polls completion.
/// </summary>
internal static class ReferenceTextPrefetchOperation
{
    /// <summary>Admits a captured operation without running DB or translation work inline.</summary>
    /// <typeparam name="TRow">The concrete canonical row type.</typeparam>
    /// <param name="writer">The process-lifetime persistence adapter.</param>
    /// <param name="broker">The shared translation broker captured at admission.</param>
    /// <param name="cache">The existing canonical cache.</param>
    /// <param name="setSelector">Selects the canonical DbSet.</param>
    /// <param name="createRow">Creates a canonical row, including sheet metadata.</param>
    /// <param name="payload">The captured sheet payload.</param>
    /// <param name="gameVersion">The captured game version.</param>
    /// <param name="source">The captured provider source language.</param>
    /// <param name="scope">The immutable reuse scope.</param>
    /// <param name="origin">The diagnostic surface identity.</param>
    /// <param name="translate">Resolves all missing fields as a single logical batch.</param>
    /// <param name="cancellationToken">Cancels this prefetch generation.</param>
    /// <param name="priority">The originating request's admission lane.</param>
    /// <returns>True when the attempt is complete; false requests a later tick retry.</returns>
    internal static Task<bool> Start<TRow>(
        ReferenceTextPersistenceWriter writer,
        QueuedTranslationBroker broker,
        ReferenceTextCacheStore<TRow> cache,
        Func<EchoglossianDbContext, DbSet<TRow>> setSelector,
        Func<string, string, int?, string?, ReferenceTextCanonicalPayload, ReferenceTextCanonicalPayload?, TRow> createRow,
        ReferenceTextCanonicalPayload payload,
        string? gameVersion,
        SourceClientLanguage source,
        TranslationReuseScope scope,
        string origin,
        Func<IReadOnlyList<TranslationField>, SourceClientLanguage, string, string?, CancellationToken, Task<TranslationFieldBatchResult>> translate,
        CancellationToken cancellationToken,
        PersistencePriority priority = PersistencePriority.Background)
        where TRow : ReferenceTextRowBase
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(false);
        }

        // The sheet-owned mutable payload never escapes the Framework capture.
        var captured = ReferenceTextCanonicalPayload.Deserialize(payload.Serialize())!;
        var probe = createRow(scope.SourceLanguageCode, scope.TargetLanguageCode,
            scope.TranslationEngine, gameVersion, captured, null);
        var cached = cache.TryFindCanonicalMatch(probe.ReferenceId, scope, gameVersion, probe.SourceContentHash!);
        if (IsComplete(captured, cached))
        {
            return Task.FromResult(true);
        }

        var admission = writer.TryFind(probe, scope, setSelector, null, out var read, priority);
        if (admission is PersistenceAdmissionStatus.RejectedCapacity or PersistenceAdmissionStatus.RejectedShutdown)
        {
            return Task.FromResult(false);
        }

        // Even an already-completed coordinator read must not execute the broker
        // continuation on the Framework thread. This is orchestration, not a queue.
        return read.ContinueWith(
            _ => CompleteAsync(writer, broker, cache, setSelector, createRow, captured,
                gameVersion, source, scope, origin, translate, read, cancellationToken, priority),
            CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();
    }

    /// <summary>Checks the required canonical fields without treating unchanged translation as missing.</summary>
    /// <param name="payload">The captured source fields.</param>
    /// <param name="row">The persisted candidate.</param>
    /// <returns>Whether every nonempty source field has a persisted translation.</returns>
    private static bool IsComplete(ReferenceTextCanonicalPayload payload, ReferenceTextRowBase? row)
    {
        return row is not null &&
            (string.IsNullOrWhiteSpace(payload.Name) || !string.IsNullOrWhiteSpace(row.TranslatedName)) &&
            (string.IsNullOrWhiteSpace(payload.Description) || !string.IsNullOrWhiteSpace(row.TranslatedDescription));
    }

    /// <summary>Finishes a captured operation off the Framework thread and observes every failure.</summary>
    /// <typeparam name="TRow">The concrete canonical row type.</typeparam>
    /// <param name="writer">The shared persistence adapter.</param>
    /// <param name="broker">The captured shared broker.</param>
    /// <param name="cache">The existing canonical cache.</param>
    /// <param name="setSelector">Selects the canonical DbSet.</param>
    /// <param name="createRow">Creates the complete row.</param>
    /// <param name="payload">The detached source payload.</param>
    /// <param name="gameVersion">The captured version.</param>
    /// <param name="source">The captured provider source.</param>
    /// <param name="scope">The captured reuse scope.</param>
    /// <param name="origin">The diagnostic surface identity.</param>
    /// <param name="translate">The batch translation resolver.</param>
    /// <param name="read">The admitted coordinator read.</param>
    /// <param name="cancellationToken">The generation cancellation token.</param>
    /// <param name="priority">The originating request's admission lane.</param>
    /// <returns>Whether the cursor may advance.</returns>
    private static async Task<bool> CompleteAsync<TRow>(
        ReferenceTextPersistenceWriter writer,
        QueuedTranslationBroker broker,
        ReferenceTextCacheStore<TRow> cache,
        Func<EchoglossianDbContext, DbSet<TRow>> setSelector,
        Func<string, string, int?, string?, ReferenceTextCanonicalPayload, ReferenceTextCanonicalPayload?, TRow> createRow,
        ReferenceTextCanonicalPayload payload,
        string? gameVersion,
        SourceClientLanguage source,
        TranslationReuseScope scope,
        string origin,
        Func<IReadOnlyList<TranslationField>, SourceClientLanguage, string, string?, CancellationToken, Task<TranslationFieldBatchResult>> translate,
        Task<PersistenceReadResult<TRow?>> read,
        CancellationToken cancellationToken,
        PersistencePriority priority)
        where TRow : ReferenceTextRowBase
    {
        try
        {
            var result = await read.WaitAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (result.Status == PersistenceCompletionStatus.Failed)
            {
                return CompleteTerminalFailure(origin, "read", result.Error);
            }

            if (result.Status != PersistenceCompletionStatus.Succeeded)
            {
                return false;
            }

            if (result.Value is { } persisted)
            {
                // Joined coordinator readers each own their generation check.
                // Cancellation of the first subscriber cannot suppress a new one.
                writer.PublishRead(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        cache.Update(persisted);
                    }
                });
            }

            if (IsComplete(payload, result.Value))
            {
                return true;
            }

            payload.TranslatedName = result.Value?.TranslatedName;
            payload.TranslatedDescription = result.Value?.TranslatedDescription;
            var fields = new List<TranslationField>(2);
            if (!string.IsNullOrWhiteSpace(payload.Name) && string.IsNullOrWhiteSpace(payload.TranslatedName))
            {
                fields.Add(new TranslationField("Name", payload.Name));
            }

            if (!string.IsNullOrWhiteSpace(payload.Description) && string.IsNullOrWhiteSpace(payload.TranslatedDescription))
            {
                fields.Add(new TranslationField("Description", payload.Description));
            }

            var key = "ReferenceTextFields|" + JsonConvert.SerializeObject(new
            {
                Origin = origin, Version = gameVersion, Scope = scope, Fields = fields,
            });
            if (!broker.TryGetCached(key, out var translated))
            {
                var completion = new TaskCompletionSource<(string? Text, bool Cancelled)>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                if (!broker.Queue(key, async () =>
                    {
                        var batch = await translate(fields, source, scope.TargetLanguageCode, origin, cancellationToken)
                            .ConfigureAwait(false);
                        var values = fields.Select(field => new TranslationField(field.Name, batch.GetTranslation(field.Name))).ToArray();
                        if (values.Any(field => string.IsNullOrWhiteSpace(field.Text)))
                        {
                            throw new InvalidOperationException("The reference-text field batch is incomplete.");
                        }

                        return TranslationFieldEnvelopeCodec.Encode(values);
                    },
                    value => completion.TrySetResult((value, false)),
                    origin,
                    cancelled => completion.TrySetResult((null, cancelled))))
                {
                    return false;
                }

                // The shared broker alone owns request timeout, retry and cooldown.
                var outcome = await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (outcome.Text is null)
                {
                    return !outcome.Cancelled;
                }

                translated = outcome.Text;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!TranslationFieldEnvelopeCodec.TryDecode(translated, fields, out var translatedFields))
            {
                return CompleteTerminalFailure(origin, "invalid cached field batch", null);
            }

            foreach (var field in fields)
            {
                var matches = translatedFields.Where(value => value.Name == field.Name).ToArray();
                if (matches.Length != 1 || string.IsNullOrWhiteSpace(matches[0].Text))
                {
                    return CompleteTerminalFailure(origin, "incomplete field batch", null);
                }

                if (field.Name == "Name")
                {
                    payload.TranslatedName = matches[0].Text;
                }
                else
                {
                    payload.TranslatedDescription = matches[0].Text;
                }
            }

            var row = createRow(scope.SourceLanguageCode, scope.TargetLanguageCode,
                scope.TranslationEngine, gameVersion, payload, payload);
            var admission = writer.TryPersist(row, setSelector,
                persisted =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        cache.Update(persisted);
                    }
                }, out var write, priority);
            if (admission is PersistenceAdmissionStatus.RejectedCapacity or PersistenceAdmissionStatus.RejectedShutdown)
            {
                return false;
            }

            var persistedResult = await write.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (persistedResult.Status == PersistenceCompletionStatus.Failed)
            {
                return CompleteTerminalFailure(origin, "write", persistedResult.Error);
            }

            return persistedResult.Status is PersistenceCompletionStatus.Succeeded or PersistenceCompletionStatus.Unchanged;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception exception)
        {
            return CompleteTerminalFailure(origin, "operation", exception);
        }
    }

    /// <summary>Logs one exhausted operation and releases its cursor without scheduling another attempt.</summary>
    /// <param name="origin">The captured diagnostic identity.</param>
    /// <param name="stage">The terminal stage.</param>
    /// <param name="exception">The terminal exception, if available.</param>
    /// <returns>True so the current background attempt advances.</returns>
    private static bool CompleteTerminalFailure(string origin, string stage, Exception? exception)
    {
        PluginRuntimeLog.Warning("ReferenceTextPrefetch", $"{origin}: terminal {stage} failure: {exception?.Message ?? "invalid result"}");
        return true;
    }
}
