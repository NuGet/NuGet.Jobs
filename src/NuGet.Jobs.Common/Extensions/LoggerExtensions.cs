using Microsoft.Extensions.Logging;
using System;

namespace NuGet.Jobs.Extensions
{
    public static class LoggerExtensions
    {
        public static IDisposable Scope(
            this ILogger logger, 
            string beginMessage, 
            string finishMessage, 
            string scopeMessage, 
            params object[] scopeMessageArgs)
        {
            return new LoggerScopeHelper(
                logger,
                beginMessage,
                finishMessage,
                scopeMessage,
                scopeMessageArgs);
        }

        private class LoggerScopeHelper : IDisposable
        {
            private readonly ILogger _logger;
            private readonly IDisposable _scope;

            private readonly string _finishMessage;

            private bool _isDisposed = false;

            public LoggerScopeHelper(
                ILogger logger,
                string beginMessage,
                string finishMessage,
                string scopeMessage,
                object[] scopeMessageArgs)
            {
                _logger = logger;
                _scope = logger.BeginScope(scopeMessage, scopeMessageArgs);
                _finishMessage = finishMessage;

                _logger.LogInformation(beginMessage);
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    _logger.LogInformation(_finishMessage);
                    _scope.Dispose();
                    _isDisposed = true;
                }
            }
        }
    }
}
