// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Linq;
using System.Web;
using Hl7.Fhir.Model;
using Microsoft.Health.Fhir.Client;
using Microsoft.Health.Fhir.Core.Models;
using Microsoft.Health.Fhir.Tests.Common.FixtureParameters;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.Health.Fhir.Tests.E2E.Rest.Search
{
    [HttpIntegrationFixtureArgumentSets(DataStore.All, Format.Json)]
    public class CanonicalSearchTests : SearchTestsBase<CanonicalSearchTestFixture>
    {
        public CanonicalSearchTests(CanonicalSearchTestFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task GivenAnObservationWithProfile_WhenSearchingByCanonicalUriVersionFragment_Then1ExpectedResultIsFound()
        {
            // We must encode '#' in the url or ASP.NET won't interpret this as part of the query string
            FhirResponse<Bundle> result = await Fixture.TestFhirClient.SearchAsync($"Observation?_profile={HttpUtility.UrlEncode(Fixture.ObservationProfileV1)}");

            // This scenario should work the same under stu3/r4+
            Assert.Collection(result.Resource.Entry, x => Assert.Equal(Fixture.ObservationProfileV1, x.Resource.Meta.Profile.Single()));
        }

        [SkippableFact]
        public async Task GivenAnObservationWithProfile_WhenSearchingByCanonicalUri_Then2ExpectedResultsAreFound()
        {
            Skip.If(ModelInfoProvider.Version == FhirSpecification.Stu3, "Stu3 doesn't separate canonical components.");

            FhirResponse<Bundle> result = await Fixture.TestFhirClient.SearchAsync($"Observation?_profile={Fixture.ObservationProfileUri}&_order=_lastModified");

            Assert.Collection(
                result.Resource.Entry,
                x => Assert.Equal(Fixture.ObservationProfileV1, x.Resource.Meta.Profile.Single()),
                x => Assert.Equal(Fixture.ObservationProfileV2, x.Resource.Meta.Profile.Single()),
                x => Assert.Equal(Fixture.ObservationProfileUriAlternate, x.Resource.Meta.Profile.First()));
        }

        [Fact]
        public async Task GivenAnObservationWithProfile_WhenSearchingByCanonicalUriVersion_Then1ExpectedResultIsFound()
        {
            FhirResponse<Bundle> result = await Fixture.TestFhirClient.SearchAsync($"Observation?_profile={Fixture.ObservationProfileUri}|2");

            // This scenario should work the same under stu3/r4+
            Assert.Collection(
                result.Resource.Entry,
                x => Assert.Equal(Fixture.ObservationProfileV2, x.Resource.Meta.Profile.Single()));
        }

        [SkippableFact]
        public async Task GivenAnObservationWithProfile_WhenSearchingByCanonicalUriFragment_Then1ExpectedResultIsFound()
        {
            Skip.If(ModelInfoProvider.Version == FhirSpecification.Stu3, "Stu3 doesn't separate canonical components.");

            FhirResponse<Bundle> result = await Fixture.TestFhirClient.SearchAsync($"Observation?_profile={Fixture.ObservationProfileUri}{HttpUtility.UrlEncode(Fixture.ObservationProfileV1Fragment)}&");

            Assert.Collection(
                result.Resource.Entry,
                x => Assert.Equal(Fixture.ObservationProfileV1, x.Resource.Meta.Profile.Single()));
        }

        [Fact]
        public async Task GivenAnObservationWithProfile_WhenSearchingByCanonicalUriMultipleProfiles_Then1ExpectedResultIsFound()
        {
            FhirResponse<Bundle> result = await Fixture.TestFhirClient.SearchAsync($"Observation?_profile={Fixture.ObservationProfileUriAlternate}");

            Assert.Collection(
                result.Resource.Entry,
                x =>
                {
                    Assert.Equal(Fixture.ObservationProfileUriAlternate, x.Resource.Meta.Profile.First());
                    Assert.Equal($"{Fixture.ObservationProfileUri}{Fixture.ObservationProfileV1Version}", x.Resource.Meta.Profile.Last());
                });
        }
    }
}
