using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Validation;

namespace Tubifarry.Indexers.SquidQobuz
{
    public class SquidQobuzIndexerSettingsValidator : AbstractValidator<SquidQobuzIndexerSettings>
    {
        public SquidQobuzIndexerSettingsValidator()
        {
            RuleFor(x => x.BaseUrl)
                .NotEmpty().WithMessage("Base URL is required.")
                .Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute))
                .WithMessage("Base URL must be a valid URL.");

            RuleFor(x => x.TokenCountry)
                .NotEmpty().WithMessage("Token Country is required.")
                .Matches("^[A-Za-z]{2}$").WithMessage("Token Country must be a two-letter country code.");
        }
    }

    public class SquidQobuzIndexerSettings : IIndexerSettings
    {
        private static readonly SquidQobuzIndexerSettingsValidator _validator = new();

        public SquidQobuzIndexerSettings()
        {
            BaseUrl = "https://qobuz.squid.wtf/api";
            RequestTimeout = 60;
            TokenCountry = "AU";
        }

        [FieldDefinition(0, Label = "Squid.wtf Qobuz API URL", Type = FieldType.Textbox, HelpText = "URL of the squid.wtf Qobuz API endpoint", Placeholder = "https://qobuz.squid.wtf/api")]
        public string BaseUrl { get; set; }

        [FieldDefinition(1, Type = FieldType.Number, Label = "Request Timeout", Unit = "seconds", HelpText = "Timeout for requests to squid.wtf Qobuz API", Advanced = true)]
        public int RequestTimeout { get; set; }

        [FieldDefinition(2, Type = FieldType.Number, Label = "Early Download Limit", Unit = "days", HelpText = "Time before release date Lidarr will download from this indexer, empty is no limit", Advanced = true)]
        public int? EarlyReleaseLimit { get; set; }

        [FieldDefinition(3, Label = "Token Country", Type = FieldType.Textbox, HelpText = "Two-letter country code sent to SquidWTF as the Token-Country header", Placeholder = "AU", Advanced = true)]
        public string TokenCountry { get; set; }

        public NzbDroneValidationResult Validate() => new(_validator.Validate(this));
    }

    public class SquidIndexerSettings : SquidQobuzIndexerSettings
    {
    }
}
