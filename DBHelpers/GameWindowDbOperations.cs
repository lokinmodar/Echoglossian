// <copyright file="GameWindowDbOperations.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.DBHelpers;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Persistence;

namespace Echoglossian;

/// <summary>Routes GameWindow runtime writes through the process-lifetime coordinator.</summary>
public partial class Echoglossian
{
    /// <summary>Gets the active runtime adapter without creating another persistence owner.</summary>
    internal static GameWindowPersistenceWriter? GameWindowWriter => activeInstance?.gameWindowPersistenceWriter;

    /// <summary>Admits a completed payload without waiting on the callback thread.</summary>
    /// <param name="row">The captured payload.</param>
    /// <param name="admitIfCurrent">The optional atomic operation-ownership guard.</param>
    /// <returns>The committed persistence status.</returns>
    internal static async Task<bool> PersistGameWindowAsync(GameWindow row, Func<Action, bool>? admitIfCurrent = null)
    {
        var writer = GameWindowWriter;
        if (writer == null || writer.ShutdownToken.IsCancellationRequested)
        {
            return false;
        }

        try
        {
            while (true)
            {
                Task<PersistenceWriteResult>? completion = null;
                var admission = PersistenceAdmissionStatus.RejectedShutdown;
                void Admit()
                {
                    admission = writer.TryPersist(row, out completion);
                }

                if (admitIfCurrent != null)
                {
                    if (!admitIfCurrent(Admit))
                    {
                        return false;
                    }
                }
                else
                {
                    Admit();
                }

                if (admission == PersistenceAdmissionStatus.RejectedCapacity)
                {
                    if (admitIfCurrent == null)
                    {
                        return false;
                    }

                    await Task.Delay(25, writer.ShutdownToken).ConfigureAwait(false);
                    continue;
                }

                var result = await completion!.ConfigureAwait(false);
                return result.Status is PersistenceCompletionStatus.Succeeded or PersistenceCompletionStatus.Unchanged;
            }
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
