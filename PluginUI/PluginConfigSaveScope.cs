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
    private static readonly AsyncLocal<Stack<Action<Config>>> Scopes = new();

    /// <summary>
    /// Pushes a configuration-save override for the current execution context.
    /// </summary>
    /// <param name="saveOverride">The callback that handles scoped saves.</param>
    /// <returns>A scope that removes the override when disposed.</returns>
    public static IDisposable Push(Action<Config> saveOverride)
    {
        var stack = Scopes.Value ??= new Stack<Action<Config>>();
        stack.Push(saveOverride);
        return new PopWhenDisposed(stack);
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
        var stack = Scopes.Value;
        if (stack is not { Count: > 0 })
        {
            return false;
        }

        stack.Peek()(config);
        return true;
    }

    /// <summary>
    /// Removes the active save override when its scope ends.
    /// </summary>
    private sealed class PopWhenDisposed : IDisposable
    {
        private readonly Stack<Action<Config>> stack;

        /// <summary>
        /// Initializes a new instance of the <see cref="PopWhenDisposed" /> class.
        /// </summary>
        /// <param name="stack">The stack containing the active override.</param>
        internal PopWhenDisposed(Stack<Action<Config>> stack)
        {
            this.stack = stack;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.stack.Pop();
        }
    }
}
