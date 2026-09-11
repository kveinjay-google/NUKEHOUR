using System.Collections.Generic;
using OpenRA;

namespace OpenRA.iOS;

// The desktop content installer switches to a separate installer mod when any
// optional package is absent. iOS receives its personal RA2 files directly in
// the app support directory, so mount what is available and continue loading.
public sealed class IosContentFileSystemLoader : IFileSystemLoader
{
    public Dictionary<string, string>? SystemPackages;
    public Dictionary<string, string>? ContentPackages;
    public Dictionary<string, string>? RequiredContentFiles;

    public void Mount(OpenRA.FileSystem.FileSystem fileSystem, ObjectCreator objectCreator)
    {
        if (SystemPackages != null)
            foreach (var package in SystemPackages)
                fileSystem.Mount(package.Key, package.Value);

        if (ContentPackages != null)
            foreach (var package in ContentPackages)
                try
                {
                    fileSystem.Mount(package.Key, package.Value);
                }
                catch
                {
                    // Missing desktop-only content must not redirect the iOS
                    // build into the unavailable content-installer mod.
                }
    }
}
