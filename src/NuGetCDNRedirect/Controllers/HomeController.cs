
using System.Web.Mvc;

namespace NuGet.Services.CDNRedirect.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return new HttpNotFoundResult("Resource not found.");
        }
    }
}