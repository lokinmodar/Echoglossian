namespace DalaMock.Shared.Interfaces;

using Dalamud.Interface.Windowing;

/// <summary>
/// Provides a factory for creating window systems. This is required to provide either a dalamud window system or a dalamock window system.
/// </summary>
public interface IWindowSystemFactory
{
    IWindowSystem Create(string? imNamespace = null);
}
