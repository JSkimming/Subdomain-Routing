using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace Subdomain.Routing.AsyncCtp
{

    /// <summary>
    /// <para>Core controller descriptor</para>
    /// </summary>
    public class CoreControllerDescriptor : ControllerDescriptor
    {
        internal delegate ActionDescriptor ActionDescriptorCreator(
            string actionName, ControllerDescriptor controllerDescriptor);

        public override Type ControllerType
        {
            get { throw new NotImplementedException(); }
        }

        public override ActionDescriptor FindAction(ControllerContext controllerContext, string actionName)
        {
            var selector = new ActionMethodSelector(controllerContext.Controller.GetType());
            var creator = selector.FindActionMethod(controllerContext, actionName);
            return creator == null ? null : creator(actionName, this);
        }

        public override ActionDescriptor[] GetCanonicalActions()
        {
            throw new NotImplementedException();
        }

        private sealed class ActionMethodSelector
        {
            public ActionMethodSelector(Type controllerType)
            {
                ControllerType = controllerType;
                PopulateLookupTables();
            }

            public Type ControllerType { get; private set; }

            public MethodInfo[] AliasedMethods { get; private set; }

            public ILookup<string, MethodInfo> NonAliasedMethods { get; private set; }

            private AmbiguousMatchException CreateAmbiguousActionMatchException(IEnumerable<MethodInfo> ambiguousMethods,
                                                                                string actionName)
            {
                var ambiguityList = CreateAmbiguousMatchList(ambiguousMethods);
                var message = String.Format(CultureInfo.CurrentUICulture,
                                            "The current request for action '{0}' on controller type '{1}' is ambiguous between the following action methods:{2}",
                                            actionName, ControllerType.Name, ambiguityList);
                return new AmbiguousMatchException(message);
            }

            private AmbiguousMatchException CreateAmbiguousMethodMatchException(IEnumerable<MethodInfo> ambiguousMethods,
                                                                                string methodName)
            {
                var ambiguityList = CreateAmbiguousMatchList(ambiguousMethods);
                var message = String.Format(CultureInfo.CurrentUICulture,
                                            "Lookup for method '{0}' on controller type '{1}' failed because of an ambiguity between the following methods:{2}",
                                            methodName, ControllerType.Name, ambiguityList);
                return new AmbiguousMatchException(message);
            }

            private static string CreateAmbiguousMatchList(IEnumerable<MethodInfo> ambiguousMethods)
            {
                var exceptionMessageBuilder = new StringBuilder();
                foreach (var methodInfo in ambiguousMethods)
                {
                    exceptionMessageBuilder.AppendLine();
                    exceptionMessageBuilder.AppendFormat(CultureInfo.CurrentUICulture, "{0} on type {1}", methodInfo,
                                                         methodInfo.DeclaringType.FullName);
                }

                return exceptionMessageBuilder.ToString();
            }

            public ActionDescriptorCreator FindActionMethod(ControllerContext controllerContext, string actionName)
            {
                var methodsMatchingName = GetMatchingAliasedMethods(controllerContext, actionName);
                methodsMatchingName.AddRange(NonAliasedMethods[actionName]);
                var finalMethods = RunSelectionFilters(controllerContext, methodsMatchingName);

                switch (finalMethods.Count)
                {
                    case 0:
                        return null;

                    case 1:
                        var entryMethod = finalMethods[0];
                        return GetActionDescriptorDelegate(entryMethod);

                    default:
                        throw CreateAmbiguousActionMatchException(finalMethods, actionName);
                }
            }

            private ActionDescriptorCreator GetActionDescriptorDelegate(MethodInfo entryMethod)
            {
                // Is this the BeginFoo() / EndFoo() pattern?
                if (entryMethod.Name.StartsWith("Begin", StringComparison.OrdinalIgnoreCase))
                {
                    var endMethodName = "End" + entryMethod.Name.Substring("Begin".Length);
                    var endMethod = GetMethodByName(endMethodName);
                    if (endMethod == null)
                    {
                        var errorMessage = String.Format(CultureInfo.CurrentUICulture,
                                                         "Could not locate a method named '{0}' on controller type '{1}'.",
                                                         endMethodName, ControllerType.FullName);
                        throw new InvalidOperationException(errorMessage);
                    }
                    throw new NotImplementedException("See Microsoft Mvc futures for implementation");
                    //return (actionName, controllerDescriptor) => new ReflectedAsyncPatternActionDescriptor(entryMethod, endMethod, actionName, controllerDescriptor);
                }

                // Is this the Foo() / FooCompleted() pattern?
                {
                    var completionMethodName = entryMethod.Name + "Completed";
                    var completionMethod = GetMethodByName(completionMethodName);
                    if (completionMethod != null)
                    {
                        throw new NotImplementedException("See Microsoft Mvc futures for implementation");
                        //return (actionName, controllerDescriptor) => new ReflectedEventPatternActionDescriptor(entryMethod, completionMethod, actionName, controllerDescriptor);
                    }
                }

                // Does Foo() return a Task that represents the continuation?
                if (typeof(Task).IsAssignableFrom(entryMethod.ReturnType))
                {
                    return
                        (actionName, controllerDescriptor) =>
                        new AsyncTaskActionDescriptor(entryMethod, controllerDescriptor);
                }

                // Fallback to synchronous method
                return
                    (actionName, controllerDescriptor) =>
                    new ReflectedActionDescriptor(entryMethod, actionName, controllerDescriptor);
            }

            private static string GetCanonicalMethodName(MethodInfo methodInfo)
            {
                var methodName = methodInfo.Name;
                return (methodName.StartsWith("Begin", StringComparison.OrdinalIgnoreCase))
                           ? methodName.Substring("Begin".Length)
                           : methodName;
            }

            internal List<MethodInfo> GetMatchingAliasedMethods(ControllerContext controllerContext, string actionName)
            {
                // find all aliased methods which are opting in to this request
                // to opt in, all attributes defined on the method must return true

                var methods = from methodInfo in AliasedMethods
                              let attrs =
                                  (ActionNameSelectorAttribute[])
                                  methodInfo.GetCustomAttributes(typeof(ActionNameSelectorAttribute), true /* inherit */)
                              where attrs.All(attr => attr.IsValidName(controllerContext, actionName, methodInfo))
                              select methodInfo;
                return methods.ToList();
            }

            private static bool IsMethodDecoratedWithAliasingAttribute(MethodInfo methodInfo)
            {
                return methodInfo.IsDefined(typeof(ActionNameSelectorAttribute), true /* inherit */);
            }

            private MethodInfo GetMethodByName(string methodName)
            {
                var methods =
                    (from MethodInfo methodInfo in
                         ControllerType.GetMember(methodName, MemberTypes.Method,
                                                  BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod |
                                                  BindingFlags.IgnoreCase)
                     where IsValidActionMethod(methodInfo, false /* stripInfrastructureMethods */)
                     select methodInfo).ToList();

                switch (methods.Count)
                {
                    case 0:
                        return null;

                    case 1:
                        return methods[0];

                    default:
                        throw CreateAmbiguousMethodMatchException(methods, methodName);
                }
            }

            private static bool IsValidActionMethod(MethodInfo methodInfo)
            {
                return IsValidActionMethod(methodInfo, true /* stripInfrastructureMethods */);
            }

            private static bool IsValidActionMethod(MethodInfo methodInfo, bool stripInfrastructureMethods)
            {
                if (methodInfo.IsSpecialName)
                {
                    // not a normal method, e.g. a constructor or an event
                    return false;
                }

                if (methodInfo.GetBaseDefinition().DeclaringType.IsAssignableFrom(typeof(AsyncController)))
                {
                    // is a method on Object, ControllerBase, Controller, or AsyncController
                    return false;
                }

                if (stripInfrastructureMethods)
                {
                    var methodName = methodInfo.Name;
                    if (methodName.StartsWith("End", StringComparison.OrdinalIgnoreCase) ||
                        methodName.EndsWith("Completed", StringComparison.OrdinalIgnoreCase))
                    {
                        // do not match EndFoo() or FooCompleted() methods, as these are infrastructure methods
                        return false;
                    }
                }

                return true;
            }

            private void PopulateLookupTables()
            {
                var allMethods =
                    ControllerType.GetMethods(BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.Public);
                var actionMethods = Array.FindAll(allMethods, IsValidActionMethod);

                AliasedMethods = Array.FindAll(actionMethods, IsMethodDecoratedWithAliasingAttribute);
                NonAliasedMethods = actionMethods.Except(AliasedMethods).ToLookup(GetCanonicalMethodName,
                                                                                  StringComparer.OrdinalIgnoreCase);
            }

            private static List<MethodInfo> RunSelectionFilters(ControllerContext controllerContext,
                                                                IEnumerable<MethodInfo> methodInfos)
            {
                // remove all methods which are opting out of this request
                // to opt out, at least one attribute defined on the method must return false

                var matchesWithSelectionAttributes = new List<MethodInfo>();
                var matchesWithoutSelectionAttributes = new List<MethodInfo>();

                foreach (var methodInfo in methodInfos)
                {
                    var attrs =
                        (ActionMethodSelectorAttribute[])
                        methodInfo.GetCustomAttributes(typeof(ActionMethodSelectorAttribute), true /* inherit */);
                    if (attrs.Length == 0)
                    {
                        matchesWithoutSelectionAttributes.Add(methodInfo);
                    }
                    else if (attrs.All(attr => attr.IsValidForRequest(controllerContext, methodInfo)))
                    {
                        matchesWithSelectionAttributes.Add(methodInfo);
                    }
                }

                // if a matching action method had a selection attribute, consider it more specific than a matching action method
                // without a selection attribute
                return (matchesWithSelectionAttributes.Count > 0)
                           ? matchesWithSelectionAttributes
                           : matchesWithoutSelectionAttributes;
            }
        }
    }
}
