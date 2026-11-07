using System;
using System.Text;
using System.Text.RegularExpressions;
using ScuroGuardiano.Slugify.ThirdParty.Apache.Lucenet;

namespace ScuroGuardiano.Slugify
{
    /// <summary>
    /// <para>
    /// Class to trasform tokens into slugs.
    /// </para>
    /// <para>
    /// Create it using <see cref="SlugifierBuilder"/>
    /// </para>
    /// </summary>
    public class Slugifier
    {
        /// <summary>
        /// Characters that are allowed in output. Every other character, that is unfoldable to those allowed characters,
        /// will be replaced by <see cref="Replacement"/> string.
        /// </summary>
        public string AllowedCharacters { get; internal set; } = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_";

        /// <summary>
        /// Replacement string for non allowed characters
        /// </summary>
        public char Replacement { get; internal set; } = '-';

        /// <summary>
        /// Whether to collapse consecutive replacements, eg: `---` -> `-`.
        /// </summary>
        public bool CollapseReplacement { get; internal set; } = false;

        /// <summary>
        /// Whether trimming is enabled
        /// </summary>
        public bool Trim { get; internal set; } = true;

        /// <summary>
        /// Whether to remove replacement at the end of the output
        /// </summary>
        public bool RemoveTrailingReplacement { get; internal set; } = true;

        /// <summary>
        /// Whether to remove replacement at the beginning of the input
        /// </summary>
        public bool RemoveLeadingReplacement { get; internal set; } = true;

        /// <summary>
        /// Whether text is converted to lowercase
        /// </summary>
        public bool ConvertToLowercase { get; internal set; } = true;

        /// <summary>
        /// Create slug from input
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public string Slugify(string input)
        {
            var asciiString = FoldToASCII(input);

            if (Trim)
            {
                asciiString = asciiString.Trim();
            }

            if (ConvertToLowercase)
            {
                asciiString = asciiString.ToLowerInvariant();
            }

            asciiString = CollapseReplacement ? ReplaceWithCollapse(asciiString) : ReplaceWithoutCollapse(asciiString);

            switch (RemoveLeadingReplacement)
            {
                case true when RemoveTrailingReplacement:
                    asciiString = asciiString.Trim(Replacement);
                    break;
                case true:
                    asciiString = asciiString.TrimStart(Replacement);
                    break;
                default:
                {
                    if (RemoveTrailingReplacement)
                    {
                        asciiString = asciiString.TrimEnd(Replacement);
                    }

                    break;
                }
            }

            return asciiString;
        }

        internal Slugifier() {}

        private string ReplaceWithoutCollapse(ReadOnlySpan<char> input)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var c in input)
            {
                if (AllowedCharacters.Contains(c))
                {
                    sb.Append(c);
                    continue;
                }
                sb.Append(Replacement);
            }

            return sb.ToString();
        }

        private string ReplaceWithCollapse(ReadOnlySpan<char> input)
        {
            StringBuilder sb = new StringBuilder();
            var wasLastReplaced = false;

            foreach (var c in input)
            {
                if (AllowedCharacters.Contains(c))
                {
                    sb.Append(c);
                    wasLastReplaced = false;
                    continue;
                }

                if (wasLastReplaced)
                {
                    continue;
                }

                sb.Append(Replacement);
                wasLastReplaced = true;
            }

            return sb.ToString();
        }

        // ReSharper disable once InconsistentNaming
        private string FoldToASCII(ReadOnlySpan<char> input)
        {
            var output = new char[input.Length * 4];
            var chars = ASCIIFolder.FoldToASCII(input, 0, output, 0, input.Length);
            return new string(output.AsSpan(0, chars));
        }
    }
}
