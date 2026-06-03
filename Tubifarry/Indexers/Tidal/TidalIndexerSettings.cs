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
                .WithMessage("Base URL must be a valid URL.")
                .When(x => x.ConnectionMode == (int)TidalConnectionMode.DirectOpenApi);

            RuleFor(x => x.MonochromeBaseUrl)
                .NotEmpty().WithMessage("Monochrome API base URL is required.")
                .Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute))
                .WithMessage("Monochrome API base URL must be a valid URL.")
                .When(x => x.ConnectionMode == (int)TidalConnectionMode.MonochromeProxy);

            RuleFor(x => x.SearchLimit)
                .InclusiveBetween(1, 100).WithMessage("Search limit must be between 1 and 100.");

            RuleFor(x => x.ClientId)
                .NotEmpty().WithMessage("TIDAL client ID is required.")
                .When(x => x.ConnectionMode == (int)TidalConnectionMode.DirectOpenApi);

            RuleFor(x => x.ClientSecret)
                .NotEmpty().WithMessage("TIDAL client secret is required.")
                .When(x => x.ConnectionMode == (int)TidalConnectionMode.DirectOpenApi);

            RuleFor(x => x.RequestTimeout)
                .InclusiveBetween(10, 300).WithMessage("Request timeout must be between 10 and 300 seconds.");
        }
    }

    public class TidalIndexerSettings : IIndexerSettings
    {
        private static readonly TidalIndexerSettingsValidator _validator = new();

        public TidalIndexerSettings()
        {
            BaseUrl = "https://openapi.tidal.com/v2";
            MonochromeBaseUrl = "https://us-west.monochrome.tf";
            CountryCode = "US";
            SearchLimit = 25;
            RequestTimeout = 60;
        }

        [FieldDefinition(0, Label = "Connection Mode", Type = FieldType.Select, SelectOptions = typeof(TidalConnectionMode), HelpText = "Use Direct OpenAPI with your TIDAL credentials or Monochrome Proxy for personal testing.")]
        public int ConnectionMode { get; set; } = (int)TidalConnectionMode.DirectOpenApi;

        [FieldDefinition(1, Label = "TIDAL API Base URL", Type = FieldType.Textbox, HelpText = "TIDAL OpenAPI v2 base URL", Placeholder = "https://openapi.tidal.com/v2")]
        public string BaseUrl { get; set; }

        [FieldDefinition(2, Label = "Monochrome API Base URL", Type = FieldType.Textbox, HelpText = "Monochrome-compatible HiFi API endpoint for proxy mode.", Placeholder = "https://us-west.monochrome.tf")]
        public string MonochromeBaseUrl { get; set; }

        [FieldDefinition(3, Label = "Country Code", Type = FieldType.Textbox, HelpText = "Two-letter country code for catalog access", Placeholder = "US")]
        public string CountryCode { get; set; }

        [FieldDefinition(4, Label = "Client ID", Type = FieldType.Textbox, HelpText = "TIDAL OpenAPI client ID. Required only in Direct OpenAPI mode.", Privacy = PrivacyLevel.ApiKey)]
        public string ClientId { get; set; } = string.Empty;

        [FieldDefinition(5, Label = "Client Secret", Type = FieldType.Password, HelpText = "TIDAL OpenAPI client secret. Required only in Direct OpenAPI mode.", Privacy = PrivacyLevel.Password)]
        public string ClientSecret { get; set; } = string.Empty;

        [FieldDefinition(6, Label = "Search Limit", Type = FieldType.Number, HelpText = "Maximum number of results to return per search", Hidden = HiddenType.Hidden, Advanced = true)]
        public int SearchLimit { get; set; }

        [FieldDefinition(7, Type = FieldType.Number, Label = "Request Timeout", Unit = "seconds", HelpText = "Timeout for requests to TIDAL API", Advanced = true)]
        public int RequestTimeout { get; set; }

        [FieldDefinition(8, Type = FieldType.Number, Label = "Early Download Limit", Unit = "days", HelpText = "Time before release date Lidarr will download from this indexer, empty is no limit", Advanced = true)]
        public int? EarlyReleaseLimit { get; set; }

        public NzbDroneValidationResult Validate() => new(_validator.Validate(this));
    }
}
