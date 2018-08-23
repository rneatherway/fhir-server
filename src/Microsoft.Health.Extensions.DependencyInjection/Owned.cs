﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using EnsureThat;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Health.Extensions.DependencyInjection
{
    /// <inheritdoc />
    public sealed class Owned<T> : IOwned<T>
    {
        private IServiceScope _scope;

        public Owned(IServiceProvider provider)
        {
            EnsureArg.IsNotNull(provider, nameof(provider));

            _scope = provider.CreateScope();
            Value = _scope.ServiceProvider.GetService<T>();
        }

        public T Value { get; }

        public void Dispose()
        {
            _scope?.Dispose();
            _scope = null;
        }
    }
}
