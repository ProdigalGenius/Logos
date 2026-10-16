using SahaNet.Application.Modules;

namespace SahaNet.Modules.Fishing;

internal sealed class FishingModuleDescriptor : IModuleDescriptor
{
    public const string ModuleKey = "fishing";

    public string Key => ModuleKey;

    public string DisplayName => "BalıkçıNet";
}
