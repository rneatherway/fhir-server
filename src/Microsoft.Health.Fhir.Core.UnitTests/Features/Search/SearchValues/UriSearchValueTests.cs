﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using Microsoft.Health.Fhir.Core.Features.Search.SearchValues;
using Microsoft.Health.Fhir.Core.Models;
using Microsoft.Health.Fhir.Tests.Common;
using Xunit;

namespace Microsoft.Health.Fhir.Core.UnitTests.Features.Search.SearchValues
{
    public class UriSearchValueTests
    {
        private const string ParamNameUri = "uri";
        private const string ParamNameS = "s";
        private readonly IModelInfoProvider _modelInfoProvider;

        public UriSearchValueTests()
        {
            _modelInfoProvider = MockModelInfoProviderBuilder.Create(FhirSpecification.R4).Build();
        }

        [Fact]
        public void GivenANullUri_WhenInitializing_ThenExceptionShouldBeThrown()
        {
            Assert.Throws<ArgumentNullException>(ParamNameUri, () => new UriSearchValue(null));
        }

        [Theory]
        [InlineData("")]
        [InlineData("    ")]
        public void GivenAnInvalidUri_WhenInitializing_ThenExceptionShouldBeThrown(string s)
        {
            Assert.Throws<ArgumentException>(ParamNameUri, () => new UriSearchValue(s));
        }

        [Fact]
        public void GivenANullString_WhenParsing_ThenExceptionShouldBeThrown()
        {
            Assert.Throws<ArgumentNullException>(ParamNameS, () => CanonicalSearchValue.Parse(null, _modelInfoProvider));
        }

        [Theory]
        [InlineData("")]
        [InlineData("    ")]
        public void GivenAnInvalidString_WhenParsing_ThenExceptionShouldBeThrown(string s)
        {
            Assert.Throws<ArgumentException>(ParamNameS, () => CanonicalSearchValue.Parse(s, _modelInfoProvider));
        }

        [Fact]
        public void GivenAValidString_WhenParsed_ThenCorrectSearchValueShouldBeReturned()
        {
            string expected = "http://uri2";

            UriSearchValue value = CanonicalSearchValue.Parse(expected, _modelInfoProvider);

            Assert.NotNull(value);
            Assert.Equal(expected, value.Uri);
        }

        [Fact]
        public void GivenASearchValue_WhenIsValidCompositeComponentIsCalled_ThenTrueShouldBeReturned()
        {
            var value = new UriSearchValue("http://uri");

            Assert.True(value.IsValidAsCompositeComponent);
        }

        [Fact]
        public void GivenASearchValue_WhenToStringIsCalled_ThenCorrectStringShouldBeReturned()
        {
            string expected = "http://uri3";

            UriSearchValue value = new UriSearchValue(expected);

            Assert.Equal(expected, value.ToString());
        }
    }
}
