using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NuGetCDNRedirect.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return new HttpNotFoundResult("Resource not found.");
        }
    }
}