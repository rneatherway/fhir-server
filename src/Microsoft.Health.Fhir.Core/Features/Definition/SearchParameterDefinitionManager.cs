﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnsureThat;
using Hl7.Fhir.ElementModel;
using Microsoft.Extensions.Hosting;
using Microsoft.Health.Fhir.Core.Features.Search;
using Microsoft.Health.Fhir.Core.Models;

namespace Microsoft.Health.Fhir.Core.Features.Definition
{
    /// <summary>
    /// Provides mechanism to access search parameter definition.
    /// </summary>
    public class SearchParameterDefinitionManager : ISearchParameterDefinitionManager, IHostedService
    {
        private readonly IModelInfoProvider _modelInfoProvider;
        private ConcurrentDictionary<string, string> _resourceTypeSearchParameterHashMap;

        public SearchParameterDefinitionManager(IModelInfoProvider modelInfoProvider)
        {
            EnsureArg.IsNotNull(modelInfoProvider, nameof(modelInfoProvider));

            _modelInfoProvider = modelInfoProvider;
            _resourceTypeSearchParameterHashMap = new ConcurrentDictionary<string, string>();
            TypeLookup = new Dictionary<string, IDictionary<string, SearchParameterInfo>>();
            UrlLookup = new Dictionary<Uri, SearchParameterInfo>();
        }

        internal IDictionary<Uri, SearchParameterInfo> UrlLookup { get; set; }

        // TypeLookup key is: Resource type, the inner dictionary key is the Search Parameter name.
        internal IDictionary<string, IDictionary<string, SearchParameterInfo>> TypeLookup { get; }

        public IEnumerable<SearchParameterInfo> AllSearchParameters => UrlLookup.Values;

        public IReadOnlyDictionary<string, string> SearchParameterHashMap
        {
            get { return new ReadOnlyDictionary<string, string>(_resourceTypeSearchParameterHashMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)); }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var bundle = SearchParameterDefinitionBuilder.ReadEmbeddedSearchParameters("search-parameters.json", _modelInfoProvider);

            SearchParameterDefinitionBuilder.Build(
                bundle.Entries.Select(e => e.Resource).ToList(),
                UrlLookup,
                TypeLookup,
                _modelInfoProvider);

            CalculateSearchParameterHash();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public IEnumerable<SearchParameterInfo> GetSearchParameters(string resourceType)
        {
            if (TypeLookup.TryGetValue(resourceType, out IDictionary<string, SearchParameterInfo> value))
            {
                return value.Values;
            }

            throw new ResourceNotSupportedException(resourceType);
        }

        public SearchParameterInfo GetSearchParameter(string resourceType, string name)
        {
            if (TypeLookup.TryGetValue(resourceType, out IDictionary<string, SearchParameterInfo> lookup) &&
                lookup.TryGetValue(name, out SearchParameterInfo searchParameter))
            {
                return searchParameter;
            }

            throw new SearchParameterNotSupportedException(resourceType, name);
        }

        public bool TryGetSearchParameter(string resourceType, string name, out SearchParameterInfo searchParameter)
        {
            searchParameter = null;

            return TypeLookup.TryGetValue(resourceType, out IDictionary<string, SearchParameterInfo> searchParameters) &&
                searchParameters.TryGetValue(name, out searchParameter);
        }

        public SearchParameterInfo GetSearchParameter(Uri definitionUri)
        {
            if (UrlLookup.TryGetValue(definitionUri, out SearchParameterInfo value))
            {
                return value;
            }

            throw new SearchParameterNotSupportedException(definitionUri);
        }

        public string GetSearchParameterHashForResourceType(string resourceType)
        {
            EnsureArg.IsNotNullOrWhiteSpace(resourceType, nameof(resourceType));

            if (_resourceTypeSearchParameterHashMap.TryGetValue(resourceType, out string hash))
            {
                return hash;
            }

            return null;
        }

        public void UpdateSearchParameterHashMap(Dictionary<string, string> updatedSearchParamHashMap)
        {
            EnsureArg.IsNotNull(updatedSearchParamHashMap, nameof(updatedSearchParamHashMap));

            foreach (KeyValuePair<string, string> kvp in updatedSearchParamHashMap)
            {
                _resourceTypeSearchParameterHashMap.AddOrUpdate(
                    kvp.Key,
                    kvp.Value,
                    (resourceType, existingValue) => kvp.Value);
            }
        }

        public void AddNewSearchParameters(IReadOnlyCollection<ITypedElement> searchParameters)
        {
            SearchParameterDefinitionBuilder.Build(
                searchParameters,
                UrlLookup,
                TypeLookup,
                _modelInfoProvider);

            CalculateSearchParameterHash();
        }

        private void CalculateSearchParameterHash()
        {
            foreach (KeyValuePair<string, IDictionary<string, SearchParameterInfo>> kvp in TypeLookup)
            {
                string searchParamHash = SearchHelperUtilities.CalculateSearchParameterHash(kvp.Value.Values);
                _resourceTypeSearchParameterHashMap.AddOrUpdate(
                    kvp.Key,
                    searchParamHash,
                    (resourceType, existingValue) => searchParamHash);
            }
        }
    }
}
