using System;
using System.Web.Mvc;
using System.Web.Routing;

namespace Subdomain.Routing.AsyncCtp
{
    /// <summary>
    /// <para>Calls service location for controllers</para>
    /// </summary>
   public class CoreControllerFactory: DefaultControllerFactory {
        /// <summary>
        ///   <para>Get instance of the controller</para>
        /// </summary>
       protected override IController GetControllerInstance(RequestContext requestContext, Type controllerType)
        {
            var iController = base.GetControllerInstance(requestContext, controllerType);

            var controller = iController as Controller;
            if (controller != null)
            {
                // IActionInvoker
                controller.ActionInvoker = DependencyResolver.Current.GetService<IActionInvoker>();

                // ITempDataProvider
                var tempDataProvider = DependencyResolver.Current.GetService<ITempDataProvider>();
                if (tempDataProvider != null)
                    controller.TempDataProvider = tempDataProvider;
            }

            return iController;
        }
   }
}
