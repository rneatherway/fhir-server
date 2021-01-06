// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using EnsureThat;
using Microsoft.Health.Fhir.Core.Models;

namespace Microsoft.Health.Fhir.Core.Features.Search.SearchValues
{
    /// <summary>
    /// Represents an URI search value.
    /// </summary>
    public class CanonicalSearchValue : UriSearchValue
    {
        private const string UrlGroupName = "url";
        private const string FragmentGroupName = "fragment";
        private const string VersionGroupName = "version";

        private static readonly Regex _canonicalFormat = new Regex($"^(?<{UrlGroupName}>http(s)?:\\/\\/[\\w.-]+[\\w\\-\\._~:/?[\\]@!\\$&'\\(\\)\\*\\+,;=.]+)(?<{VersionGroupName}>\\|[\\w]+)?(?<{FragmentGroupName}>\\#[\\w]+)?$", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="CanonicalSearchValue"/> class.
        /// </summary>
        /// <param name="uri">The URI value.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054:Uri parameters should not be strings", Justification = "Value is passed in from user")]
        public CanonicalSearchValue(string uri)
        {
            EnsureArg.IsNotNullOrWhiteSpace(uri, nameof(uri));

            // More information: https://www.hl7.org/fhir/references.html#canonical-fragments
            // Parse url in format of:
            // http://example.com/folder|4#fragment

            Match result = _canonicalFormat.Match(uri);

            if (result.Success)
            {
                Uri = ValueWhenNotNullOrWhiteSpace(result.Groups[UrlGroupName].Value);
                Fragment = ValueWhenNotNullOrWhiteSpace(result.Groups[FragmentGroupName].Value?.TrimStart('#'));
                Version = ValueWhenNotNullOrWhiteSpace(result.Groups[VersionGroupName].Value?.TrimStart('|'));
            }
            else
            {
                // Treat as literal
                Uri = uri;
            }

            string ValueWhenNotNullOrWhiteSpace(string value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }

                return null;
            }
        }

        public string Version { get; set; }

        public string Fragment { get; set; }

        /// <summary>
        /// When true the search value has Canonical components Uri, Version and/or Fragment.
        /// When false the search value contains only Uri.
        /// </summary>
        public bool IsCanonicalValue
        {
            get
            {
                return !string.IsNullOrWhiteSpace(Version) || !string.IsNullOrWhiteSpace(Fragment);
            }
        }

        /// <summary>
        /// Parses the string value to an instance of <see cref="CanonicalSearchValue"/>.
        /// </summary>
        /// <param name="s">The string to be parsed.</param>
        /// <param name="modelInfoProvider">FHIR Model Info provider</param>
        /// <returns>An instance of <see cref="CanonicalSearchValue"/>.</returns>
        public static UriSearchValue Parse(string s, IModelInfoProvider modelInfoProvider)
        {
            EnsureArg.IsNotNullOrWhiteSpace(s, nameof(s));
            EnsureArg.IsNotNull(modelInfoProvider, nameof(modelInfoProvider));

            if (modelInfoProvider.Version == FhirSpecification.Stu3 || !_canonicalFormat.IsMatch(s))
            {
                return new UriSearchValue(s);
            }

            return new CanonicalSearchValue(s);
        }

        /// <inheritdoc />
        public override void AcceptVisitor(ISearchValueVisitor visitor)
        {
            EnsureArg.IsNotNull(visitor, nameof(visitor));

            visitor.Visit(this);
        }

        public override bool Equals([AllowNull] ISearchValue other)
        {
            if (other == null)
            {
                return false;
            }

            return ToString() == other.ToString();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            if (string.IsNullOrEmpty(Version))
            {
                return Uri;
            }

            if (string.IsNullOrEmpty(Fragment))
            {
                return string.Concat(Uri, "|", Version);
            }

            return string.Concat(Uri, "|", Version, "#", Fragment);
        }
    }
}
