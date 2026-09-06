// <copyright file="ReferenceTextPrefetchCompletion.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
/// Publishes one operation outcome before its completion task finishes so the
/// Framework cursor can poll without synchronously retrieving a task result.
/// </summary>
internal sealed class ReferenceTextPrefetchCompletion
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReferenceTextPrefetchCompletion" /> class.
    /// </summary>
    /// <param name="operation">The already scheduled reference-text operation.</param>
    internal ReferenceTextPrefetchCompletion(Task<bool> operation)
    {
        this.Completion = this.ObserveAsync(operation);
    }

    /// <summary>Gets the task that completes after the outcome is published.</summary>
    internal Task<bool> Completion { get; }

    /// <summary>
    /// Gets a value indicating whether the cursor may advance; read only after
    /// <see cref="Completion" /> has completed.
    /// </summary>
    internal bool Succeeded { get; private set; }

    /// <summary>Observes success, failure, and cancellation without blocking a caller.</summary>
    /// <param name="operation">The already scheduled operation.</param>
    /// <returns>The published cursor-advancement outcome.</returns>
    private async Task<bool> ObserveAsync(Task<bool> operation)
    {
        try
        {
            this.Succeeded = await operation.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Failed and canceled tasks retain their cursor for paced retry.
            this.Succeeded = false;
        }

        return this.Succeeded;
    }
}
