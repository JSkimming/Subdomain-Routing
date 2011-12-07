using System.Web.Mvc;

namespace Subdomain.Routing.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index(string tenant, string catalogue, string style)
        {
            ViewData["Message"] = string.Format("tenant = '{0}', catalogue = '{1}', style = '{2}'.", tenant, catalogue, style);
            return View();
        }
    }
}
