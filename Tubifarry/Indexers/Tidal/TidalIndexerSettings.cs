using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Validation;

namespace Tubifarry.Indexers.Tidal
{
    public class TidalIndexerSettingsValidator : AbstractValidator<TidalIndexerSettings>
    {
        public TidalIndexerSettingsValidator()
        {
            RuleFor(x => x.BaseUrl)
                .NotEmpty().WithMessage("Base URL is required.")
                .Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute))
                .WithMessage("Base URL must be a valid URL.");

            RuleFor(x => x.SearchLimit)
                .InclusiveBetween(1, 100).WithMessage("Search limit must be between 1 and 100.");

            RuleFor(x => x.RequestTimeout)
                .InclusiveBetween(10, 300).WithMessage("Request timeout must be between 10 and 300 seconds.");
        }
    }

    public class TidalIndexerSettings : IIndexerSettings
    {
        private static readonly TidalIndexerSettingsValidator _validator = new();

        public TidalIndexerSettings()
        {
            BaseUrl = "https://api.tidal.com/v1";
            CountryCode = "US";
            SearchLimit = 25;
            RequestTimeout = 60;
        }

        [FieldDefinition(0, Label = "TIDAL API Base URL", Type = FieldType.Textbox, HelpText = "TIDAL API v1 base URL", Placeholder = "https://api.tidal.com/v1")]
        public string BaseUrl { get; set; }

        [FieldDefinition(1, Label = "Country Code", Type = FieldType.Textbox, HelpText = "Two-letter country code for catalog access", Placeholder = "US")]
        public string CountryCode { get; set; }

        [FieldDefinition(2, Label = "Search Limit", Type = FieldType.Number, HelpText = "Maximum number of results to return per search", Hidden = HiddenType.Hidden, Advanced = true)]
        public int SearchLimit { get; set; }

        [FieldDefinition(3, Type = FieldType.Number, Label = "Request Timeout", Unit = "seconds", HelpText = "Timeout for requests to TIDAL API", Advanced = true)]
        public int RequestTimeout { get; set; }

        [FieldDefinition(4, Type = FieldType.Number, Label = "Early Download Limit", Unit = "days", HelpText = "Time before release date Lidarr will download from this indexer, empty is no limit", Advanced = true)]
        public int? EarlyReleaseLimit { get; set; }

        public NzbDroneValidationResult Validate() => new(_validator.Validate(this));
    }
}
