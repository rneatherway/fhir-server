﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using EnsureThat;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using Microsoft.Health.Fhir.Core.Extensions;
using Microsoft.Health.Fhir.Core.Features.Definition;
using Microsoft.Health.Fhir.Core.Features.Search.SearchValues;
using static Hl7.Fhir.Model.SearchParameter;

namespace Microsoft.Health.Fhir.Core.Features.Search.Expressions
{
    /// <summary>
    /// A builder used to build expression from the search value.
    /// </summary>
    public class SearchValueExpressionBuilder : ISearchValueExpressionBuilder
    {
        private static readonly Dictionary<SearchParamType, Func<string, ISearchValue>> ParserDictionary = new Dictionary<SearchParamType, Func<string, ISearchValue>>()
        {
            { SearchParamType.Date, DateTimeSearchValue.Parse },
            { SearchParamType.Number, NumberSearchValue.Parse },
            { SearchParamType.Quantity, QuantitySearchValue.Parse },
            { SearchParamType.Reference, ReferenceSearchValue.Parse },
            { SearchParamType.String, StringSearchValue.Parse },
            { SearchParamType.Token, TokenSearchValue.Parse },
            { SearchParamType.Uri, UriSearchValue.Parse },
        };

        private static readonly Tuple<string, SearchComparator>[] SearchParamComparators = Enum.GetValues(typeof(SearchComparator))
            .Cast<SearchComparator>()
            .Select(e => Tuple.Create(e.GetLiteral(), e)).ToArray();

        private readonly ISearchParameterDefinitionManager _searchParameterDefinitionManager;

        public SearchValueExpressionBuilder(
            ISearchParameterDefinitionManager searchParameterDefinitionManager)
        {
            EnsureArg.IsNotNull(searchParameterDefinitionManager, nameof(searchParameterDefinitionManager));

            _searchParameterDefinitionManager = searchParameterDefinitionManager;
        }

        public Expression Build(
            SearchParameter searchParameter,
            SearchModifierCode? modifier,
            string value)
        {
            EnsureArg.IsNotNull(searchParameter, nameof(searchParameter));
            Debug.Assert(
                modifier == null || Enum.IsDefined(typeof(SearchModifierCode), modifier.Value),
                "Invalid modifier.");
            EnsureArg.IsNotNullOrWhiteSpace(value, nameof(value));

            IEnumerable<Expression> outputExpressions = null;

            if (modifier == SearchModifierCode.Missing)
            {
                // We have to handle :missing modifier specially because if :missing modifier is specified,
                // then the value is a boolean string indicating whether the parameter is missing or not instead of
                // the search value type associated with the search parameter.
                if (!bool.TryParse(value, out bool isMissing))
                {
                    // An invalid value was specified.
                    throw new InvalidSearchOperationException(Core.Resources.InvalidValueTypeForMissingModifier);
                }

                return Expression.Missing(searchParameter.Name, isMissing);
            }
            else if (modifier == SearchModifierCode.Text)
            {
                // We have to handle :text modifier specially because if :text modifier is supplied for token search param,
                // then we want to search the display text using the specified text, and therefore
                // we don't want to actually parse the specified text into token.
                if (searchParameter.Type != SearchParamType.Token)
                {
                    throw new InvalidSearchOperationException(
                        string.Format(CultureInfo.InvariantCulture, Core.Resources.ModifierNotSupported, modifier, searchParameter.Name));
                }

                outputExpressions = Expression.Contains(FieldName.TokenText, null, value, true).AsEnumerable();
            }
            else
            {
                // Build the expression for based on the search value.
                if (searchParameter.Type == SearchParamType.Composite)
                {
                    IReadOnlyList<string> compositeValueParts = value.SplitByCompositeSeparator();

                    if (compositeValueParts.Count > searchParameter.Component.Count)
                    {
                        throw new InvalidSearchOperationException(
                            string.Format(CultureInfo.InvariantCulture, Core.Resources.NumberOfCompositeComponentsExceeded, searchParameter.Name));
                    }

                    var compositeExpressions = new Expression[compositeValueParts.Count];

                    for (int i = 0; i < compositeValueParts.Count; i++)
                    {
                        ComponentComponent component = searchParameter.Component[i];

                        // Find the corresponding search parameter info.
                        SearchParameter componentSearchParameter = _searchParameterDefinitionManager.GetSearchParameter(component.Definition.Url);

                        string componentValue = compositeValueParts[i];

                        compositeExpressions[i] = Build(
                            componentSearchParameter,
                            modifier: null,
                            componentIndex: i,
                            value: componentValue);
                    }

                    outputExpressions = compositeExpressions;
                }
                else
                {
                    outputExpressions = Build(
                        searchParameter,
                        modifier,
                        componentIndex: null,
                        value: value)
                        .AsEnumerable();
                }
            }

            // Add the parameter name matching expression which is common to all search values.
            outputExpressions = Expression.Equals(FieldName.ParamName, null, searchParameter.Name).AsEnumerable().Concat(
                outputExpressions);

            return Expression.And(
                outputExpressions.ToArray());
        }

        private static Expression Build(
            SearchParameter searchParameter,
            SearchModifierCode? modifier,
            int? componentIndex,
            string value)
        {
            // By default, the comparator is equal.
            SearchComparator comparator = SearchComparator.Eq;

            if (searchParameter.Type == SearchParamType.Date ||
                searchParameter.Type == SearchParamType.Number ||
                searchParameter.Type == SearchParamType.Quantity)
            {
                // If the search parameter type supports comparator, parse the comparator (if present).
                Tuple<string, SearchComparator> matchedComparator = SearchParamComparators.FirstOrDefault(
                    s => value.StartsWith(s.Item1, StringComparison.Ordinal));

                if (matchedComparator != null)
                {
                    comparator = matchedComparator.Item2;
                    value = value.Substring(matchedComparator.Item1.Length);
                }
            }

            // Parse the value.
            Func<string, ISearchValue> parser = ParserDictionary[searchParameter.Type.Value];

            // Build the expression.
            SearchValueExpressionBuilderHelper helper = new SearchValueExpressionBuilderHelper();

            // If the value contains comma, then we need to convert it into in expression.
            // But in this case, the user cannot specify prefix.
            IReadOnlyList<string> parts = value.SplitByOrSeparator();

            if (parts.Count == 1)
            {
                // This is a single value expression.
                ISearchValue searchValue = parser(value);

                return helper.Build(
                    searchParameter.Name,
                    modifier,
                    comparator,
                    componentIndex,
                    searchValue);
            }
            else
            {
                if (comparator != SearchComparator.Eq)
                {
                    throw new InvalidSearchOperationException(Core.Resources.SearchComparatorNotSupported);
                }

                // This is a multiple value expression.
                Expression[] expressions = parts.Select(part =>
                {
                    ISearchValue searchValue = parser(part);

                    return helper.Build(
                        searchParameter.Name,
                        modifier,
                        comparator,
                        componentIndex,
                        searchValue);
                }).ToArray();

                return Expression.Or(expressions);
            }
        }
    }
}
