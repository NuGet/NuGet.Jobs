// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Web.Mvc;
using System.Web.Routing;

namespace NuGet.Services.CDNRedirect
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            // Ensure that SSLv3 is disabled and that TLS v1.2 is enabled.
            ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            // Ensure that certificate validation check for online revocations.
            ServicePointManager.CheckCertificateRevocationList = true;

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }
    }
}
