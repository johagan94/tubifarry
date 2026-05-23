using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;
using NzbDrone.Core.Validation.Paths;

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
        }
    }

    public class TidalProviderSettings : IProviderConfig
    {
        private static readonly TidalProviderSettingsValidator Validator = new();

        [FieldDefinition(0, Label = "Download Path", Type = FieldType.Path, HelpText = "Directory where downloaded files will be saved")]
        public string DownloadPath { get; set; } = string.Empty;

        [FieldDefinition(1, Label = "Download Quality", Type = FieldType.Select, SelectOptions = typeof(TidalDownloadQuality), HelpText = "Preferred audio quality for downloads. Hi-Res Lossless requires TIDAL HiFi Plus subscription equivalent.")]
        public int DownloadQuality { get; set; } = (int)TidalDownloadQuality.LOSSLESS;

        [FieldDefinition(2, Label = "Country Code", Type = FieldType.Textbox, HelpText = "Two-letter country code for catalog access", Placeholder = "US")]
        public string CountryCode { get; set; } = "US";

        [FieldDefinition(3, Type = FieldType.Number, Label = "Connection Retries", HelpText = "Number of times to retry failed connections", Advanced = true)]
        public int ConnectionRetries { get; set; } = 3;

        [FieldDefinition(4, Type = FieldType.Number, Label = "Max Parallel Downloads", HelpText = "Maximum number of downloads that can run simultaneously")]
        public int MaxParallelDownloads { get; set; } = 2;

        [FieldDefinition(5, Label = "Max Download Speed", Type = FieldType.Number, HelpText = "Set to 0 for unlimited speed. Limits download speed per file.", Unit = "KB/s", Advanced = true)]
        public int MaxDownloadSpeed { get; set; } = 0;

        [FieldDefinition(6, Label = "Output Format", Type = FieldType.Select, SelectOptions = typeof(TidalOutputFormat), HelpText = "Output audio format. FLAC preserves lossless quality.", Advanced = true)]
        public int OutputFormat { get; set; } = (int)TidalOutputFormat.FLAC;

        [FieldDefinition(7, Label = "MP3 Bitrate", Type = FieldType.Select, SelectOptions = typeof(TidalMp3Bitrate), HelpText = "Bitrate for MP3 output when format is set to MP3.", Advanced = true)]
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
