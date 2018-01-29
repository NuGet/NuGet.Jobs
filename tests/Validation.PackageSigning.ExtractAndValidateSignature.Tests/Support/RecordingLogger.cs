﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Validation.PackageSigning.ExtractAndValidateSignature.Tests
{
    public class RecordingLogger<T> : ILogger<T>
    {
        private readonly ILogger<T> _inner;
        private readonly List<string> _messages = new List<string>();

        public RecordingLogger(ILogger<T> inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public IReadOnlyList<string> Messages => _messages;

        public IDisposable BeginScope<TState>(TState state)
        {
            return _inner.BeginScope(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return _inner.IsEnabled(logLevel);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            _messages.Add(formatter(state, exception));
            _inner.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}
