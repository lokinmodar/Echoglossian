// <copyright file="DbFirstGameWindowAddonHandler.Translation.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.Helpers;

namespace Echoglossian.NativeUI.AddonHandlers.Common;

/// <summary>
///     Completes translation and persistence outside the native UI unsafe context.
/// </summary>
public abstract partial class DbFirstGameWindowAddonHandler
{
    /// <summary>Observes supplemental persistence without blocking addon callbacks or retiring uncommitted work.</summary>
    /// <param name="scope">The captured reuse scope.</param>
    /// <param name="sourceOperation">The captured operation owner.</param>
    /// <param name="originalPayload">The original payload.</param>
    /// <param name="translatedPayload">The supplemental translated payload.</param>
    /// <param name="payloadKey">The existing in-flight payload identity.</param>
    /// <param name="classJobId">The captured class/job discriminator.</param>
    /// <returns>The observed persistence and refresh completion.</returns>
    internal async Task PersistSupplementalGameWindowPayloadAsync(
        TranslationReuseScope scope,
        DbFirstSourceOperation sourceOperation,
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload,
        string payloadKey,
        uint? classJobId)
    {
        // Cleanup can reopen the same payload before its old persistence completion retires.
        var inFlightKey = $"{payloadKey}|supplemental:{this.supplementalPersistenceOwner:N}:{sourceOperation.Generation}";
        if (!this.retryGate.TrySetRetry(sourceOperation, DateTime.UtcNow + RetryInterval) ||
            !InFlightPayloads.TryAdd(inFlightKey, 0))
        {
            return;
        }

        var shutdownToken = Echoglossian.GameWindowWriter?.ShutdownToken ?? CancellationToken.None;
        try
        {
            var persisted = false;
            try
            {
                persisted = await this.PersistResolvedGameWindowPayloadAsync(
                    scope,
                    sourceOperation,
                    originalPayload,
                    translatedPayload,
                    classJobId).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                PluginRuntimeLog.Warning("GameWindowPersistence", $"Supplemental persistence failed: {exception.Message}");
            }

            if (persisted)
            {
                this.retryGate.TryRunIfCurrent(sourceOperation, () => ClearFailedPayloadRetry(payloadKey));
            }
            else
            {
                if (!this.retryGate.TrySetRetry(sourceOperation, DateTime.UtcNow + RetryInterval))
                {
                    return;
                }

                // A delayed refresh also retries applied states that would otherwise short-circuit PreDraw.
                await Task.Delay(RetryInterval, shutdownToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
        {
            return;
        }
        finally
        {
            InFlightPayloads.TryRemove(inFlightKey, out _);
        }

        this.RequestRefresh(sourceOperation);
    }

    /// <summary>
    ///     Waits for a committed payload while preserving atomic operation ownership at admission.
    /// </summary>
    /// <param name="scope">The immutable operation scope.</param>
    /// <param name="sourceOperation">The captured operation ownership.</param>
    /// <param name="originalPayload">The original payload.</param>
    /// <param name="translatedPayload">The translated payload.</param>
    /// <param name="classJobId">The captured class/job discriminator.</param>
    /// <returns>Whether the dedicated store or coordinator persisted the payload.</returns>
    private protected async Task<bool> PersistResolvedGameWindowPayloadAsync(
        TranslationReuseScope scope,
        DbFirstSourceOperation sourceOperation,
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload,
        uint? classJobId)
    {
        var dedicated = false;
        if (!this.retryGate.TryRunIfCurrent(
                sourceOperation,
                () => dedicated = this.TryPersistDedicatedPayload(scope, originalPayload, translatedPayload),
                clearRetry: false))
        {
            return false;
        }

        if (dedicated)
        {
            return true;
        }

        return await this.PersistResolvedGenericGameWindowPayload(
            scope,
            originalPayload,
            translatedPayload,
            classJobId,
            action => this.retryGate.TryRunIfCurrent(sourceOperation, action, clearRetry: false)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Translates and persists one non-structured
    ///     <see cref="GameWindow" /> payload.
    /// </summary>
    /// <param name="sourceLanguage">The operation-captured source identity.</param>
    /// <param name="originalPayload">The original payload to translate.</param>
    /// <returns>
    ///     A task whose result is <see langword="true" /> when translation
    ///     completed successfully; otherwise <see langword="false" />.
    /// </returns>
    private protected virtual async Task<bool>
        TranslateAndPersistGameWindowPayloadAsync(
            TranslationReuseScope scope,
            DbFirstSourceOperation sourceOperation,
            SourceClientLanguage sourceLanguage,
            DbFirstGameWindowPayload originalPayload)
    {
        if (!scope.TranslationEngine.HasValue)
        {
            return false;
        }

        var translatorResolution = this.translationService
            .CaptureTranslatorResolution(
                scope.TranslationEngine.Value,
                TranslationSurfaceGroup.Default);
        var translatedResult = await Task.Run(() => GenericAddonHandlerHelper
            .TranslatePayloadAsync(
                originalPayload.AtkValues,
                originalPayload.StringArrayValues,
                originalPayload.TextNodes,
                originalPayload.AtkValues,
                originalPayload.StringArrayValues,
                originalPayload.TextNodes,
                sourceLanguage,
                scope.TargetLanguageCode,
                this.translationService,
                translatorResolution)).ConfigureAwait(false);
        if (!translatedResult.HasValue)
        {
            return false;
        }

        var translatedPayload = new DbFirstGameWindowPayload(
            translatedResult.Value.AtkValues,
            translatedResult.Value.StringArrayValues,
            translatedResult.Value.TextNodes);
        return await this.PersistResolvedGameWindowPayloadAsync(
            scope,
            sourceOperation,
            originalPayload,
            translatedPayload,
            this.GetPersistedGameWindowClassJobId(
                originalPayload,
                translatedPayload)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Translates and persists one structured payload while keeping the
    ///     provider and persistence source identities separate.
    /// </summary>
    /// <param name="sourceLanguage">The operation-captured source identity.</param>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <returns>The persisted row, or no value when translation is incomplete.</returns>
    private async Task<StringArrayDatas?> TranslateAndPersistStructuredPayloadAsync(
        TranslationReuseScope scope,
        DbFirstSourceOperation sourceOperation,
        SourceClientLanguage sourceLanguage,
        StringArrayStructuredPayload originalPayload)
    {
        if (!scope.TranslationEngine.HasValue)
        {
            return null;
        }

        var translatorResolution = this.translationService
            .CaptureTranslatorResolution(
                scope.TranslationEngine.Value,
                TranslationSurfaceGroup.Default);
        var translatedPayload = await Task.Run(() => DbFirstStructuredStringArrayHelper
            .TranslatePayloadAsync(
                originalPayload,
                this.translationService,
                sourceLanguage,
                scope.TargetLanguageCode,
                translatorResolution)).ConfigureAwait(false);
        if (!DbFirstStructuredStringArrayHelper.TryProjectTranslatedPayload(
                originalPayload,
                translatedPayload,
                out _))
        {
            return null;
        }

        var row = StringArrayDataPersistenceHelper.CreateCanonicalRow(
            originalPayload.Type,
            scope.SourceLanguageCode,
            scope.TargetLanguageCode,
            scope.TranslationEngine.GetValueOrDefault(),
            GetGameVersion(),
            originalPayload,
            translatedPayload);
        return this.retryGate.TryRunIfCurrent(
            sourceOperation,
            () =>
            {
                _ = StringArrayDataPersistenceHelper.InsertStringArrayData(
                    ConfigDirectory,
                    row);
            })
            ? row
            : null;
    }

    /// <summary>
    ///     Queues background translation and DB save when needed.
    /// </summary>
    /// <param name="sourceLanguage">The operation-captured source identity.</param>
    /// <param name="payload">The original payload.</param>
    /// <param name="payloadKey">The stable payload key.</param>
    /// <param name="sourceOperation">The source-owned operation token.</param>
    private void QueueTranslationIfNeeded(
        SourceClientLanguage sourceLanguage,
        TranslationReuseScope scope,
        DbFirstGameWindowPayload payload,
        string payloadKey,
        DbFirstSourceOperation sourceOperation,
        StringArrayStructuredPayload? originalStructuredPayload = null)
    {
        if (!InFlightPayloads.TryAdd(payloadKey, 0))
        {
            return;
        }

        var structuredTask = originalStructuredPayload != null
            ? this.TranslateAndPersistStructuredPayloadAsync(
                scope,
                sourceOperation,
                sourceLanguage,
                originalStructuredPayload)
            : null;
        var gameWindowTask = originalStructuredPayload == null
            ? this.TranslateAndPersistGameWindowPayloadAsync(
                scope,
                sourceOperation,
                sourceLanguage,
                payload)
            : null;
        _ = Task.Run(async () =>
        {
            try
            {
                bool succeeded;
                if (structuredTask != null)
                {
                    var row = await structuredTask.ConfigureAwait(false);
                    succeeded = row != null;
                    if (row != null)
                    {
                        StringArrayDataCacheManager.Update(row);
                    }
                }
                else
                {
                    succeeded = await gameWindowTask!.ConfigureAwait(false);
                }

                if (succeeded)
                {
                    ClearFailedPayloadRetry(payloadKey);
                    this.RequestRefresh(sourceOperation);
                }
                else
                {
                    MarkFailedPayloadRetry(payloadKey);
                }
            }
            catch (Exception)
            {
                MarkFailedPayloadRetry(payloadKey);
            }
            finally
            {
                InFlightPayloads.TryRemove(payloadKey, out _);
            }
        });
    }
}
