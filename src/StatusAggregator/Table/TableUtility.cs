using NuGet.Services.Status;

namespace StatusAggregator.Table
{
    public static class TableUtility
    {
        public static string ToRowKeySafeComponentPath(string componentPath)
        {
            return componentPath.Replace(Constants.ComponentPathDivider, '_');
        }
    }
}
