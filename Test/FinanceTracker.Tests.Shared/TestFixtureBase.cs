// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace FinanceTracker.Tests.Shared;

public class TestFixtureBase
{
    protected internal CancellationTokenSource _cancellationTokenSource;

    public TestFixtureBase()
    {
        _cancellationTokenSource = new CancellationTokenSource();
    }
}
