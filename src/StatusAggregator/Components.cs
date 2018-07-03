using System.Linq;

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
                            new TreeComponent("USNC", "Primary region"),
                            new TreeComponent("USSC", "Backup region")
                        }),
                    new TreeComponent(
                        "Restore",
                        "Downloading and installing packages from NuGet",
                        new[]
                        {
                            new TreeComponent(
                                "V3",
                                "Restore using the V3 API",
                                new[]
                                {
                                    new TreeComponent("Global", "V3 restore for users outside of China"),
                                    new TreeComponent("China", "V3 restore for users inside China")
                                }),
                            new TreeComponent("V2", "Restore using the V2 API")
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
                                    new TreeComponent("USNC", "Primary region"),
                                    new TreeComponent("USSC", "Backup region")
                                }),
                            new PrimarySecondaryComponent(
                                "Asia",
                                "Search for packages inside Asia",
                                new[]
                                {
                                    new TreeComponent("EA", "Primary region"),
                                    new TreeComponent("SEA", "Backup region")
                                })
                        }),
                    new TreeComponent("Package Publishing", "Uploading new packages to NuGet.org")
                });

        public static ISubComponent Get(string path)
        {
            var componentPathParts = path.Split(SubComponent.ComponentPathDivider);

            if (componentPathParts.First() != Root.Name)
            {
                return null;
            }

            ISubComponent currentComponent = new SubComponent(Root);
            foreach (var componentPathPart in componentPathParts.Skip(1))
            {
                currentComponent = currentComponent.SubComponents.FirstOrDefault(c => c.Name == componentPathPart);

                if (currentComponent == null)
                {
                    break;
                }
            }

            return currentComponent;
        }
    }
}
