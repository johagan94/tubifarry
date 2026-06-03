using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;
using NzbDrone.Core.Validation.Paths;
using Tubifarry.Indexers.Tidal;

namespace Tubifarry.Download.Clients.Tidal
{
    public class TidalProviderSettingsValidator : AbstractValidator<TidalProviderSettings>
    {
        public TidalProviderSettingsValidator()
        {
            RuleFor(x => x.DownloadPath)
                .IsValidPath()
                .WithMessage("Download path must be a valid directory.");

            RuleFor(x => x.ConnectionRetries)
                .GreaterThanOrEqualTo(1)
                .LessThanOrEqualTo(10)
                .WithMessage("Connection retries must be between 1 and 10.");

            RuleFor(x => x.MaxParallelDownloads)
                .GreaterThanOrEqualTo(1)
                .LessThanOrEqualTo(5)
                .WithMessage("Max parallel downloads must be between 1 and 5.");

            RuleFor(x => x.MaxDownloadSpeed)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Max download speed must be greater than or equal to 0.")
                .LessThanOrEqualTo(100_000)
                .WithMessage("Max download speed must be less than or equal to 100 MB/s.");

            RuleFor(x => x.ClientId)
                .NotEmpty().WithMessage("TIDAL client ID is required.")
                .When(x => x.ConnectionMode == (int)TidalConnectionMode.DirectOpenApi);

            RuleFor(x => x.ClientSecret)
                .NotEmpty().WithMessage("TIDAL client secret is required.")
                .When(x => x.ConnectionMode == (int)TidalConnectionMode.DirectOpenApi);

            RuleFor(x => x.MonochromeBaseUrl)
                .NotEmpty().WithMessage("Monochrome API base URL is required.")
                .Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute))
                .WithMessage("Monochrome API base URL must be a valid URL.")
                .When(x => x.ConnectionMode == (int)TidalConnectionMode.MonochromeProxy);
        }
    }

    public class TidalProviderSettings : IProviderConfig
    {
        private static readonly TidalProviderSettingsValidator Validator = new();

        [FieldDefinition(0, Label = "Download Path", Type = FieldType.Path, HelpText = "Directory where downloaded files will be saved")]
        public string DownloadPath { get; set; } = string.Empty;

        [FieldDefinition(1, Label = "Download Quality", Type = FieldType.Select, SelectOptions = typeof(TidalDownloadQuality), HelpText = "Preferred audio quality for downloads. Hi-Res Lossless requires TIDAL HiFi Plus subscription equivalent.")]
        public int DownloadQuality { get; set; } = (int)TidalDownloadQuality.LOSSLESS;

        [FieldDefinition(2, Label = "Connection Mode", Type = FieldType.Select, SelectOptions = typeof(TidalConnectionMode), HelpText = "Use Direct OpenAPI with your TIDAL credentials or Monochrome Proxy for personal testing.")]
        public int ConnectionMode { get; set; } = (int)TidalConnectionMode.DirectOpenApi;

        [FieldDefinition(3, Label = "Monochrome API Base URL", Type = FieldType.Textbox, HelpText = "Monochrome-compatible HiFi API endpoint for proxy mode.", Placeholder = "https://us-west.monochrome.tf")]
        public string MonochromeBaseUrl { get; set; } = "https://us-west.monochrome.tf";

        [FieldDefinition(4, Label = "Client ID", Type = FieldType.Textbox, HelpText = "TIDAL OpenAPI client ID. Required only in Direct OpenAPI mode.", Privacy = PrivacyLevel.ApiKey)]
        public string ClientId { get; set; } = string.Empty;

        [FieldDefinition(5, Label = "Client Secret", Type = FieldType.Password, HelpText = "TIDAL OpenAPI client secret. Required only in Direct OpenAPI mode.", Privacy = PrivacyLevel.Password)]
        public string ClientSecret { get; set; } = string.Empty;

        [FieldDefinition(6, Label = "Country Code", Type = FieldType.Textbox, HelpText = "Two-letter country code for catalog access", Placeholder = "US")]
        public string CountryCode { get; set; } = "US";

        [FieldDefinition(7, Type = FieldType.Number, Label = "Connection Retries", HelpText = "Number of times to retry failed connections", Advanced = true)]
        public int ConnectionRetries { get; set; } = 3;

        [FieldDefinition(8, Type = FieldType.Number, Label = "Max Parallel Downloads", HelpText = "Maximum number of downloads that can run simultaneously")]
        public int MaxParallelDownloads { get; set; } = 2;

        [FieldDefinition(9, Label = "Max Download Speed", Type = FieldType.Number, HelpText = "Set to 0 for unlimited speed. Limits download speed per file.", Unit = "KB/s", Advanced = true)]
        public int MaxDownloadSpeed { get; set; } = 0;

        [FieldDefinition(10, Label = "Output Format", Type = FieldType.Select, SelectOptions = typeof(TidalOutputFormat), HelpText = "Output audio format. FLAC preserves lossless quality.", Advanced = true)]
        public int OutputFormat { get; set; } = (int)TidalOutputFormat.FLAC;

        [FieldDefinition(11, Label = "MP3 Bitrate", Type = FieldType.Select, SelectOptions = typeof(TidalMp3Bitrate), HelpText = "Bitrate for MP3 output when format is set to MP3.", Advanced = true)]
        public int Mp3Bitrate { get; set; } = (int)TidalMp3Bitrate.k320;

        public NzbDroneValidationResult Validate() => new(Validator.Validate(this));
    }

    public enum TidalDownloadQuality
    {
        [FieldOption("Hi-Res Lossless (FLAC 24-bit/96kHz)")]
        HI_RES_LOSSLESS = 0,
        [FieldOption("Lossless (FLAC 16-bit/44.1kHz)")]
        LOSSLESS = 1,
        [FieldOption("High (AAC 320kbps)")]
        HIGH = 2,
        [FieldOption("Low (AAC 96kbps)")]
        LOW = 3
    }

    public enum TidalOutputFormat
    {
        [FieldOption("FLAC")]
        FLAC = 0,
        [FieldOption("MP3")]
        MP3 = 1,
        [FieldOption("M4A (AAC)")]
        M4A = 2
    }

    public enum TidalMp3Bitrate
    {
        [FieldOption("320 kbps")]
        k320 = 0,
        [FieldOption("256 kbps")]
        k256 = 1,
        [FieldOption("192 kbps")]
        k192 = 2,
        [FieldOption("128 kbps")]
        k128 = 3
    }
}
