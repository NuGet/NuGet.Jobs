using NuGet.Services.Status;

namespace StatusAggregator
{
    public static class Components
    {
        public static IComponent Root = new TreeComponent(
                "NuGet",
                "",
                new IComponent[]
                {
                    new PrimarySecondaryComponent(
                        "NuGet.org",
                        "Browsing the Gallery website",
                        new[]
                        {
                            new LeafComponent("USNC", "Primary region"),
                            new LeafComponent("USSC", "Backup region")
                        }),
                    new TreeComponent(
                        "Restore",
                        "Downloading and installing packages from NuGet",
                        new IComponent[]
                        {
                            new TreeComponent(
                                "V3",
                                "Restore using the V3 API",
                                new[]
                                {
                                    new LeafComponent("Global", "V3 restore for users outside of China"),
                                    new LeafComponent("China", "V3 restore for users inside China")
                                }),
                            new LeafComponent("V2", "Restore using the V2 API")
                        }),
                    new TreeComponent(
                        "Search",
                        "Searching for new and existing packages in Visual Studio or the Gallery website",
                        new[]
                        {
                            new PrimarySecondaryComponent(
                                "Global",
                                "Search for packages outside Asia",
                                new[]
                                {
                                    new LeafComponent("USNC", "Primary region"),
                                    new LeafComponent("USSC", "Backup region")
                                }),
                            new PrimarySecondaryComponent(
                                "Asia",
                                "Search for packages inside Asia",
                                new[]
                                {
                                    new LeafComponent("EA", "Primary region"),
                                    new LeafComponent("SEA", "Backup region")
                                })
                        }),
                    new LeafComponent("Package Publishing", "Uploading new packages to NuGet.org")
                });
    }
}
