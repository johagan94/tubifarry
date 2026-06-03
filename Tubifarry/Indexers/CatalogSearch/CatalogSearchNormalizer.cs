using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Tubifarry.Indexers.CatalogSearch
{
    public static partial class CatalogSearchNormalizer
    {
        private static readonly char[] DashCharacters = ['\u2010', '\u2011', '\u2012', '\u2013', '\u2014', '\u2212'];
        private static readonly char[] ApostropheCharacters = ['\u2018', '\u2019', '\u201A', '\u201B', '`'];

        public static bool IsArtistMatch(string? candidate, string? expected, IEnumerable<string> aliases)
        {
            string normalizedCandidate = NormalizeForMatch(candidate);
            if (string.IsNullOrEmpty(normalizedCandidate))
                return false;

            IEnumerable<string?> names = new[] { expected }.Concat(aliases);
            return names
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(NormalizeForMatch)
                .Any(name => name.Length != 0 && name == normalizedCandidate);
        }

        public static bool IsAlbumMatch(string? candidate, IEnumerable<string> expectedAlbums)
        {
            string normalizedCandidate = NormalizeForMatch(candidate);
            if (string.IsNullOrEmpty(normalizedCandidate))
                return false;

            return expectedAlbums
                .Where(album => !string.IsNullOrWhiteSpace(album))
                .Select(NormalizeForMatch)
                .Any(album => album.Length != 0 && album == normalizedCandidate);
        }

        public static IEnumerable<string> BuildQueryVariants(string? artist, string? album = null)
        {
            HashSet<string> variants = new(StringComparer.OrdinalIgnoreCase);

            AddQuery(variants, album, artist);

            string? normalizedArtist = NormalizeForSearch(artist);
            string? normalizedAlbum = NormalizeForSearch(album);
            AddQuery(variants, normalizedAlbum, normalizedArtist);

            string? punctuationlessArtist = StripPunctuationForSearch(artist);
            string? punctuationlessAlbum = StripPunctuationForSearch(album);
            AddQuery(variants, punctuationlessAlbum, punctuationlessArtist);

            if (!string.IsNullOrWhiteSpace(album))
                AddQuery(variants, StripBracketSuffix(album), artist);

            if (string.IsNullOrWhiteSpace(album))
                AddQuery(variants, null, artist);

            return variants;
        }

        public static string NormalizeForMatch(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string text = NormalizeCommonCharacters(value);
            text = RemoveDiacritics(text);
            text = RemoveJoinerPunctuationRegex().Replace(text, "");
            text = MatchPunctuationRegex().Replace(text, " ");
            text = CollapseWhitespaceRegex().Replace(text, " ").Trim();
            return text.ToLowerInvariant();
        }

        private static string? NormalizeForSearch(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string text = NormalizeCommonCharacters(value);
            text = RemoveDiacritics(text);
            text = CollapseWhitespaceRegex().Replace(text, " ").Trim();
            return text.Length == 0 ? null : text;
        }

        private static string? StripPunctuationForSearch(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string text = NormalizeForSearch(value) ?? string.Empty;
            text = RemoveJoinerPunctuationRegex().Replace(text, "");
            text = MatchPunctuationRegex().Replace(text, " ");
            text = CollapseWhitespaceRegex().Replace(text, " ").Trim();
            return text.Length == 0 ? null : text;
        }

        private static void AddQuery(HashSet<string> variants, string? album, string? artist)
        {
            string query = string.Join(' ', new[] { album, artist }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => part!.Trim()));

            if (!string.IsNullOrWhiteSpace(query))
                variants.Add(query);
        }

        private static string StripBracketSuffix(string value)
        {
            string text = value.Trim();
            while (true)
            {
                int start = text.LastIndexOfAny(['[', '(', '{']);
                if (start < 0)
                    return text;

                string candidate = text[..start].TrimEnd();
                if (candidate.Length == 0)
                    return text;

                text = candidate;
            }
        }

        private static string NormalizeCommonCharacters(string value)
        {
            StringBuilder builder = new(value.Length);
            foreach (char c in value)
            {
                if (DashCharacters.Contains(c))
                    builder.Append('-');
                else if (ApostropheCharacters.Contains(c))
                    builder.Append('\'');
                else
                    builder.Append(c);
            }

            return builder.ToString();
        }

        private static string RemoveDiacritics(string value)
        {
            string decomposed = value.Normalize(NormalizationForm.FormD);
            StringBuilder builder = new(decomposed.Length);

            foreach (char c in decomposed)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category != UnicodeCategory.NonSpacingMark &&
                    category != UnicodeCategory.SpacingCombiningMark &&
                    category != UnicodeCategory.EnclosingMark)
                {
                    builder.Append(c);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        [GeneratedRegex(@"[^\p{L}\p{Nd}]+", RegexOptions.Compiled)]
        private static partial Regex MatchPunctuationRegex();

        [GeneratedRegex(@"['.]", RegexOptions.Compiled)]
        private static partial Regex RemoveJoinerPunctuationRegex();

        [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
        private static partial Regex CollapseWhitespaceRegex();
    }
}
