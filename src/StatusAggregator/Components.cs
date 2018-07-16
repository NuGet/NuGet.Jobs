using NuGet.Services.Status;

namespace StatusAggregator
{
    public static class Components
    {
        public static IComponent CreateNuGetServiceRootComponent()
        {
            return new TreeComponent(
                "NuGet",
                "",
                new IComponent[]
                {
                    new PrimarySecondaryComponent(
                        "NuGet.org",
                        "Browsing the Gallery website",
                        new[]
                        {
                            new LeafComponent("North Central US", "Primary region"),
                            new LeafComponent("South Central US", "Backup region")
                        }),
                    new TreeComponent(
                        "Restore",
                        "Downloading and installing packages from NuGet",
                        new IComponent[]
                        {
                            new TreeComponent(
                                "V3 Protocol",
                                "Restore using the V3 API",
                                new[]
                                {
                                    new LeafComponent("Global", "V3 restore for users outside of China"),
                                    new LeafComponent("China", "V3 restore for users inside China")
                                }),
                            new LeafComponent("V2 Protocol", "Restore using the V2 API")
                        }),
                    new TreeComponent(
                        "Search",
                        "Searching for new and existing packages in Visual Studio or the Gallery website",
                        new[]
                        {
                            new PrimarySecondaryComponent(
                                "Global",
                                "Search for packages outside China",
                                new[]
                                {
                                    new LeafComponent("North Central US", "Primary region"),
                                    new LeafComponent("South Central US", "Backup region")
                                }),
                            new PrimarySecondaryComponent(
                                "China",
                                "Search for packages inside China",
                                new[]
                                {
                                    new LeafComponent("East Asia", "Primary region"),
                                    new LeafComponent("Southeast Asia", "Backup region")
                                })
                        }),
                    new LeafComponent("Package Publishing", "Uploading new packages to NuGet.org")
                });
        }
    }
}
