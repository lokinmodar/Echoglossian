// <copyright file="PluginConfigSaveScope.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI;

/// <summary>
/// Redirects configuration saves within the current asynchronous execution
/// context.
/// </summary>
public static class PluginConfigSaveScope
{
    private static readonly AsyncLocal<ScopeFrame?> CurrentScope = new();

    /// <summary>
    /// Pushes a configuration-save override for the current execution context.
    /// </summary>
    /// <param name="saveOverride">The callback that handles scoped saves.</param>
    /// <returns>A scope that removes the override when disposed.</returns>
    public static IDisposable Push(Action<Config> saveOverride)
    {
        var frame = new ScopeFrame(saveOverride, CurrentScope.Value);
        CurrentScope.Value = frame;
        return new ScopeToken(frame);
    }

    /// <summary>
    /// Attempts to handle a configuration save through the active override.
    /// </summary>
    /// <param name="config">The configuration being saved.</param>
    /// <returns>
    /// <see langword="true" /> when an active override handled the save;
    /// otherwise, <see langword="false" />.
    /// </returns>
    public static bool TrySave(Config config)
    {
        var scope = CurrentScope.Value;
        if (scope is null)
        {
            return false;
        }

        scope.SaveOverride(config);
        return true;
    }

    /// <summary>
    /// Determines whether an immutable scope chain contains a specific frame.
    /// </summary>
    /// <param name="current">The current scope frame.</param>
    /// <param name="target">The frame to find.</param>
    /// <returns>
    /// <see langword="true" /> when <paramref name="target" /> is present;
    /// otherwise, <see langword="false" />.
    /// </returns>
    private static bool ContainsScope(ScopeFrame? current, ScopeFrame target)
    {
        for (var scope = current; scope is not null; scope = scope.Parent)
        {
            if (ReferenceEquals(scope, target))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Represents one immutable save override in the current execution flow.
    /// </summary>
    /// <param name="SaveOverride">The callback that handles scoped saves.</param>
    /// <param name="Parent">The next outer scope, if one exists.</param>
    private sealed record ScopeFrame(
        Action<Config> SaveOverride,
        ScopeFrame? Parent);

    /// <summary>
    /// Removes one save-override frame when its scope ends.
    /// </summary>
    private sealed class ScopeToken : IDisposable
    {
        private readonly ScopeFrame frame;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeToken" /> class.
        /// </summary>
        /// <param name="frame">The frame removed when this token is disposed.</param>
        internal ScopeToken(ScopeFrame frame)
        {
            this.frame = frame;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            var current = CurrentScope.Value;
            if (ReferenceEquals(current, this.frame))
            {
                CurrentScope.Value = this.frame.Parent;
                return;
            }

            if (ContainsScope(current, this.frame))
            {
                throw new InvalidOperationException(
                    "Plugin configuration save scopes must be disposed in reverse order.");
            }
        }
    }
}
