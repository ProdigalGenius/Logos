using SahaNet.Application.Modules;

namespace SahaNet.Modules.Scrap;

internal sealed class ScrapModuleDescriptor : IModuleDescriptor
{
    public const string ModuleKey = "scrap";

    public string Key => ModuleKey;

    public string DisplayName => "HurdaNet";
}
