using System.Web.Mvc;

namespace Subdomain.Routing.Controllers
{
    public class Test1Controller : Controller
    {
        //
        // GET: /Test1/

        public ActionResult Index(string tenant)
        {
            ViewData["Message"] = string.Format("And the tenant is '{0}'.", tenant);
            return View();
        }

        public ActionResult Another(string tenant)
        {
            ViewData["Message"] = string.Format("And the tenant is '{0}'.", tenant);
            return View();
        }
    }
}
