// <copyright file="DalamudUiFontRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Interface.ManagedFontAtlas;

using Echoglossian.PluginUI.Helpers;

namespace Echoglossian.PluginUI.Runtime;

/// <summary>
/// Bridges shared UI renderers to the plugin's Dalamud-managed font handles.
/// </summary>
internal sealed class DalamudUiFontRuntime : IUiFontRuntime, IDisposable
{
    private readonly UINewFontHandler fontHandler;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DalamudUiFontRuntime"/> class.
    /// </summary>
    /// <param name="fontHandler">The plugin-managed font handles.</param>
    public DalamudUiFontRuntime(UINewFontHandler fontHandler)
    {
        this.fontHandler = fontHandler;
    }

    /// <inheritdoc />
    public IDisposable Push(UiFontKind fontKind)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        var handle = fontKind == UiFontKind.General
            ? this.fontHandler.GeneralFontHandle
            : this.fontHandler.LanguageFontHandle;
        handle.Push();
        return new FontPopScope(handle);
    }

    /// <summary>
    /// Marks the runtime unavailable for future font pushes.
    /// </summary>
    public void Dispose()
    {
        this.disposed = true;
    }

    /// <summary>
    /// Restores the previous ImGui font stack entry exactly once.
    /// </summary>
    private sealed class FontPopScope : IDisposable
    {
        private IFontHandle? handle;

        /// <summary>
        /// Initializes a new instance of the <see cref="FontPopScope"/> class.
        /// </summary>
        /// <param name="handle">The pushed font handle.</param>
        public FontPopScope(IFontHandle handle)
        {
            this.handle = handle;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            var pushedHandle = Interlocked.Exchange(ref this.handle, null);
            pushedHandle?.Pop();
        }
    }
}
