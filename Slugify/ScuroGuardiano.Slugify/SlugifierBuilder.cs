using System;

namespace ScuroGuardiano.Slugify
{
    /// <summary>
    /// Used to build <see cref="Slugifier"/>
    /// </summary>
    public class SlugifierBuilder
    {
        private char _replacement = '-';
        private bool _collapseReplacement = false;
        private bool _trim = true;
        private bool _convertToLowercase = true;
        private string _allowedCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_";
        private bool _removeLeadingReplacements = true;
        private bool _removeTrailingReplacements = true;

        /// <summary>
        /// Provide string that will be used to replace spaces and other forbidden characters
        /// </summary>
        /// <param name="replacement">Replacement string. '-' is used as default</param>
        /// <returns></returns>
        public SlugifierBuilder ReplaceWith(char replacement)
        {
            _replacement  = replacement;
            return this;
        }

        /// <summary>
        /// <para>
        /// Whether to collapse consecutive replacements, eg: `---` -> `-`.
        /// </para>
        /// <para>
        /// By default collapsing is disabled
        /// </para>
        /// </summary>
        /// <returns></returns>
        public SlugifierBuilder CollapseReplacement()
        {
            _collapseReplacement = true;
            return this;
        }

        /// <summary>
        /// Disable trimming spaces at the start and the end or text to slugify
        /// </summary>
        /// <returns></returns>
        public SlugifierBuilder NoTrim()
        {
            _trim = false;
            return this;
        }

        /// <summary>
        /// Disables convertion to lowercase
        /// </summary>
        /// <returns></returns>
        public SlugifierBuilder WithoutLowercaseConvertion()
        {
            _convertToLowercase = false;
            return this;
        }

        /// <summary>
        /// Sets which characters are allowed
        /// </summary>
        /// <param name="allowedCharacters"></param>
        /// <returns></returns>
        public SlugifierBuilder WithAllowedCharacters(ReadOnlySpan<char> allowedCharacters)
        {
            _allowedCharacters = allowedCharacters.ToString();
            return this;
        }

        /// <summary>
        /// Sets which characters are allowed
        /// </summary>
        /// <param name="allowedCharacters"></param>
        /// <returns></returns>
        public SlugifierBuilder WithAllowedCharacters(string allowedCharacters)
        {
            _allowedCharacters = allowedCharacters;
            return this;
        }

        /// <summary>
        /// Disables trimming leading replacements. Eg. <c>?x</c> will become <c>-x</c> instead of <c>x</c>
        /// </summary>
        /// <returns></returns>
        public SlugifierBuilder WithoutRemovingLeadingReplacements()
        {
            _removeLeadingReplacements = false;
            return this;
        }

        /// <summary>
        /// Disables trimming trailing replacements. Eg. <c>x?</c> will become <c>x-</c> instead of <c>x</c>
        /// </summary>
        /// <returns></returns>
        public SlugifierBuilder WithoutRemovingTrailingReplacements()
        {
            _removeTrailingReplacements = false;
            return this;
        }

        /// <summary>
        /// Builds <see cref="Slugifier"/>
        /// </summary>
        /// <returns></returns>
        public Slugifier Build()
        {
            return new Slugifier()
            {
                CollapseReplacement = _collapseReplacement,
                Trim = _trim,
                ConvertToLowercase = _convertToLowercase,
                Replacement = _replacement,
                AllowedCharacters = _allowedCharacters,
                RemoveLeadingReplacement = _removeLeadingReplacements,
                RemoveTrailingReplacement = _removeTrailingReplacements
            };
        }
    }
}
