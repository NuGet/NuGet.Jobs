using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NuGetCDNRedirect.Controllers
{
    public class StatusController : Controller
    {
        // GET: Status
        public ActionResult Index()
        {
            return View();
        }
    }
}