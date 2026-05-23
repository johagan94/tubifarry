using Tubifarry.Download.Base;

namespace Tubifarry.Download.Clients.Tidal
{
    public record TidalDownloadOptions : BaseDownloadOptions
    {
        public string CountryCode { get; set; } = "US";
        public int DownloadQuality { get; set; } = 1;
        public int OutputFormat { get; set; } = 0;
        public int Mp3Bitrate { get; set; } = 0;

        public TidalDownloadOptions() : base() { }

        protected TidalDownloadOptions(TidalDownloadOptions options) : base(options)
        {
            CountryCode = options.CountryCode;
            DownloadQuality = options.DownloadQuality;
            OutputFormat = options.OutputFormat;
            Mp3Bitrate = options.Mp3Bitrate;
        }
    }
}
