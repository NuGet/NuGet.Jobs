﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NuGet.Services.AzureSearch;
using NuGet.Services.AzureSearch.SearchService;
using System.Net;

namespace NuGet.Services.SearchService
{
    public class ApiExceptionFilterAttribute : ExceptionFilterAttribute
    {
        public override void OnException(ExceptionContext context)
        {
            switch (context.Exception)
            {
                case AzureSearchException _:
                    context.Result = new JsonResult(new ErrorResponse("The service is unavailable."))
                    {
                        StatusCode = (int)HttpStatusCode.ServiceUnavailable
                    };
                    break;

                case InvalidSearchRequestException isre:
                    context.Result = new JsonResult(new ErrorResponse(isre.Message))
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest
                    };
                    break;
            }
        }

        private class ErrorResponse
        {
            public ErrorResponse(string message)
            {
                Message = message;
            }

            public bool Success { get; } = false;
            public string Message { get; }
        }
    }
}
