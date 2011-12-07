using System.Web.Mvc;
using System.Web.Routing;
using Subdomain.Routing.Routing;

namespace Subdomain.Routing
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801

    public class MvcApplication : System.Web.HttpApplication
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }

        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.Add("DomainTenantRoute", new DomainRoute(
                "{tenant}.testdomin.com",     // Domain with parameters
                "{controller}/{action}/{id}", // URL with parameters
                new { controller = "Home", action = "Index", id = UrlParameter.Optional, tenant = UrlParameter.Optional}  // Parameter defaults
            ));

            routes.Add("DomainTenantCatalogueRoute", new DomainRoute(
                "{tenant}-cat_{catalogue}.testdomin.com",     // Domain with parameters
                "{controller}/{action}/{id}", // URL with parameters
                new { controller = "Home", action = "Index", id = UrlParameter.Optional, tenant = UrlParameter.Optional, catalogue = UrlParameter.Optional }  // Parameter defaults
            ));

            routes.Add("DomainTenantStyleRoute", new DomainRoute(
                "{tenant}-style_{style}.testdomin.com",     // Domain with parameters
                "{controller}/{action}/{id}", // URL with parameters
                new { controller = "Home", action = "Index", id = UrlParameter.Optional, tenant = UrlParameter.Optional, style = UrlParameter.Optional }  // Parameter defaults
            ));

            routes.Add("DomainTenantCatalogueStyleRoute", new DomainRoute(
                "{tenant}-cat_{catalogue}-style_{style}.testdomin.com",     // Domain with parameters
                "{controller}/{action}/{id}", // URL with parameters
                new { controller = "Home", action = "Index", id = UrlParameter.Optional, tenant = UrlParameter.Optional, catalogue = UrlParameter.Optional, style = UrlParameter.Optional }  // Parameter defaults
            ));

            routes.MapRoute(
                "Default", // Route name
                "{controller}/{action}/{id}", // URL with parameters
                new { controller = "Home", action = "Index", id = UrlParameter.Optional } // Parameter defaults
            );

        }

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();

            RegisterGlobalFilters(GlobalFilters.Filters);
            RegisterRoutes(RouteTable.Routes);
        }
    }
}
