using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;
using NzbDrone.Core.Validation.Paths;

namespace Tubifarry.Download.Clients.SquidQobuz
{
    public class SquidQobuzProviderSettingsValidator : AbstractValidator<SquidQobuzProviderSettings>
    {
        public SquidQobuzProviderSettingsValidator()
        {
            RuleFor(x => x.BaseUrl)
                .NotEmpty().WithMessage("Base URL is required.")
                .Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute))
                .WithMessage("Base URL must be a valid URL.");

            RuleFor(x => x.DownloadPath)
                .IsValidPath()
                .WithMessage("Download path must be a valid directory.");

            RuleFor(x => x.RequestTimeout)
                .GreaterThanOrEqualTo(10)
                .LessThanOrEqualTo(300)
                .WithMessage("Request timeout must be between 10 and 300 seconds.");
        }
    }

    public class SquidQobuzProviderSettings : IProviderConfig
    {
        private static readonly SquidQobuzProviderSettingsValidator Validator = new();

        [FieldDefinition(0, Label = "Download Path", Type = FieldType.Path, HelpText = "Directory where downloaded files will be saved")]
        public string DownloadPath { get; set; } = string.Empty;

        [FieldDefinition(1, Label = "Squid.wtf Qobuz URL", Type = FieldType.Textbox, HelpText = "URL of the squid.wtf Qobuz instance (region-specific)", Placeholder = "https://eu.qobuz.squid.wtf/api")]
        public string BaseUrl { get; set; } = "https://eu.qobuz.squid.wtf/api";

        [FieldDefinition(2, Label = "Quality", Type = FieldType.Select, SelectOptions = typeof(SquidQobuzQuality), HelpText = "Preferred download quality")]
        public int Quality { get; set; } = (int)SquidQobuzQuality.LOSSLESS;

        [FieldDefinition(3, Type = FieldType.Number, Label = "Request Timeout", Unit = "seconds", HelpText = "Timeout for HTTP requests", Advanced = true)]
        public int RequestTimeout { get; set; } = 60;

        public NzbDroneValidationResult Validate() => new(Validator.Validate(this));
    }

    public enum SquidQobuzQuality
    {
        [FieldOption("FLAC 24-bit/192kHz")]
        HI_RES = 0,
        [FieldOption("FLAC 16-bit/44.1kHz")]
        LOSSLESS = 1,
        [FieldOption("MP3 320kbps")]
        MP3 = 2
    }
}
