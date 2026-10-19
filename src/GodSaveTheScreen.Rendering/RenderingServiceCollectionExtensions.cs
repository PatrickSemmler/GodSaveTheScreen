using Microsoft.Extensions.DependencyInjection;

namespace GodSaveTheScreen.Rendering;

public static class RenderingServiceCollectionExtensions
{
    /// <summary>Registrierungen des Rendering-Projekts (folgen ab M4/M5: Exporter, Codecs).</summary>
    public static IServiceCollection AddRendering(this IServiceCollection services) => services;
}
