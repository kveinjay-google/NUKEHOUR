using OpenRA.Mods.Cnc.FileSystem;

namespace OpenRA.iOS;

// OpenRA discovers these loaders from the mod manifest at runtime. Referencing
// their constructors here makes the iOS AOT compiler emit the entry points
// needed by reflection without changing the desktop mod-loading path.
internal static class AotPreserve
{
    static readonly MixLoader Mix = new();
    static readonly AudioBagLoader AudioBag = new();
    static readonly IosContentFileSystemLoader ContentInstaller = new();

    public static void Touch()
    {
        GC.KeepAlive(Mix);
        GC.KeepAlive(AudioBag);
        GC.KeepAlive(ContentInstaller);
    }
}
