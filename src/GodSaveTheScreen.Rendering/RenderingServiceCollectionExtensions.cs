using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Project;
using Microsoft.Extensions.DependencyInjection;

namespace GodSaveTheScreen.Rendering;

public static class RenderingServiceCollectionExtensions
{
    public static IServiceCollection AddRendering(this IServiceCollection services)
    {
        TextLayout.Install();
        services.AddSingleton<IImageCodecService, ImageCodecService>();
        services.AddSingleton<IDocumentExporter, DocumentExporter>();
        services.AddSingleton<IProjectService, ProjectService>();
        return services;
    }
}
