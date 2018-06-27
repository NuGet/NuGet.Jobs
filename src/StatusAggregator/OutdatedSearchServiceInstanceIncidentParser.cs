namespace StatusAggregator
{
    public class OutdatedSearchServiceInstanceIncidentParser : EnvironmentIncidentParser
    {
        private const string SubtitleRegEx = "A search service instance is using an outdated index!";

        private readonly string _environment;

        public OutdatedSearchServiceInstanceIncidentParser(string environment)
            : base(SubtitleRegEx, environment)
        {
            _environment = environment;
        }
    }
}
