using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Mvc.Async;

namespace Subdomain.Routing.AsyncCtp
{
    /// <summary>
    ///   <para>AsyncActionDescriptor for methods returning a task</para>
    /// </summary>
    public class AsyncTaskActionDescriptor : AsyncActionDescriptor
    {

        private readonly MethodInfo _actionMethod;
        private readonly MethodDispatcher _actionMethodDispatcher;
        private readonly ControllerDescriptor _controllerDescriptor;

        /// <summary>
        /// <para>Create object</para>
        /// </summary>
        public AsyncTaskActionDescriptor(MethodInfo actionMethod, ControllerDescriptor controllerDescriptor)
        {
            _actionMethod = actionMethod;
            _actionMethodDispatcher = new MethodDispatcher(actionMethod);
            _controllerDescriptor = controllerDescriptor;
        }

        public override string ActionName
        {
            get { throw new NotImplementedException(); }
        }

        public override ControllerDescriptor ControllerDescriptor
        {
            get { return _controllerDescriptor; }
        }

        public override ParameterDescriptor[] GetParameters()
        {
            return _actionMethod
                .GetParameters()
                .Select(pi => new ReflectedParameterDescriptor(pi, this))
                .Cast<ParameterDescriptor>()
                .ToArray();
        }

        //slightly altered from ReflectedDelegatePatternActionDescriptor in Asp.net mvc futures.
        public override IAsyncResult BeginExecute(ControllerContext controllerContext,
                                                  IDictionary<string, object> parameters, AsyncCallback callback,
                                                  object state)
        {
            var task = Task<object>.Factory.StartNew(x =>
            {
                var setupParametersInfos = _actionMethod.GetParameters();
                var rawSetupParameterValues =
                    from parameterInfo in setupParametersInfos
                    select
                        ExtractParameterFromDictionary(parameterInfo,
                                                       parameters,
                                                       _actionMethod);
                var setupParametersArray = rawSetupParameterValues.ToArray();

                var returnedDelegate =
                    _actionMethodDispatcher.Execute(
                        controllerContext.Controller, setupParametersArray);

                if (returnedDelegate != null &&
                    returnedDelegate.GetType().IsGenericType &&
                    typeof(Task<>).IsAssignableFrom(
                        returnedDelegate.GetType().GetGenericTypeDefinition()))
                {
                    dynamic taskResult = returnedDelegate;
                    return taskResult.Result;
                }

                return null;
            }, state);

            if (callback != null) task.ContinueWith(res => callback(task));

            return task;
        }

        public override object EndExecute(IAsyncResult asyncResult)
        {
            return ((Task<object>)asyncResult).Result;
        }

        //copied from internal AsyncActionDescriptor implementation in Asp.net mvc futures.
        private static object ExtractParameterFromDictionary(ParameterInfo parameterInfo,
                                                             IDictionary<string, object> parameters,
                                                             MethodInfo methodInfo)
        {
            object value;

            if (!parameters.TryGetValue(parameterInfo.Name, out value))
            {
                // the key should always be present, even if the parameter value is null
                var message = String.Format(CultureInfo.CurrentUICulture,
                                            "The parameters dictionary does not contain an entry for parameter '{0}' of type '{1}' for method '{2}' in '{3}'. The dictionary must contain an entry for each parameter, even parameters with null values.",
                                            parameterInfo.Name, parameterInfo.ParameterType, methodInfo,
                                            methodInfo.DeclaringType);
                throw new ArgumentException(message, "parameters");
            }

            if (value == null &&
                (parameterInfo.ParameterType.IsValueType
                ||parameterInfo.ParameterType.IsNullable()))
            {
                // tried to pass a null value for a non-nullable parameter type
                var message = String.Format(CultureInfo.CurrentUICulture,
                                            "The parameters dictionary contains a null entry for parameter '{0}' of non-nullable type '{1}' for method '{2}' in '{3}'. To make a parameter optional its type should be either a reference type or a Nullable type.",
                                            parameterInfo.Name, parameterInfo.ParameterType, methodInfo,
                                            methodInfo.DeclaringType);
                throw new ArgumentException(message, "parameters");
            }

            if (value != null && !parameterInfo.ParameterType.IsInstanceOfType(value))
            {
                // value was supplied but is not of the proper type
                var message = String.Format(CultureInfo.CurrentUICulture,
                                            "The parameters dictionary contains an invalid entry for parameter '{0}' for method '{1}' in '{2}'. The dictionary contains a value of type '{3}', but the parameter requires a value of type '{4}'.",
                                            parameterInfo.Name, methodInfo, methodInfo.DeclaringType, value.GetType(),
                                            parameterInfo.ParameterType);
                throw new ArgumentException(message, "parameters");
            }

            return value;
        }

        #region Nested type: MethodDispatcher

        internal sealed class MethodDispatcher
        {
            private readonly MethodExecutor _executor;

            public MethodDispatcher(MethodInfo methodInfo)
            {
                _executor = GetExecutor(methodInfo);
                MethodInfo = methodInfo;
            }

            public MethodInfo MethodInfo { get; private set; }

            public object Execute(object target, object[] parameters)
            {
                return _executor(target, parameters);
            }

            private static MethodExecutor GetExecutor(MethodInfo methodInfo)
            {
                // Parameters to executor
                var targetParameter = Expression.Parameter(typeof(object), "target");
                var parametersParameter = Expression.Parameter(typeof(object[]), "parameters");

                // Build parameter list
                var parameters = new List<Expression>();
                var paramInfos = methodInfo.GetParameters();
                for (var i = 0; i < paramInfos.Length; i++)
                {
                    var paramInfo = paramInfos[i];
                    var valueObj = Expression.ArrayIndex(parametersParameter, Expression.Constant(i));
                    var valueCast = Expression.Convert(valueObj, paramInfo.ParameterType);

                    // valueCast is "(Ti) parameters[i]"
                    parameters.Add(valueCast);
                }

                // Call method
                var targetCast = (!methodInfo.IsStatic)
                                     ? Expression.Convert(targetParameter, methodInfo.ReflectedType)
                                     : null;
                var methodCall = Expression.Call(targetCast, methodInfo, parameters);

                // methodCall is "((TTarget) target) method((T0) parameters[0], (T1) parameters[1], ...)"
                // Create function
                if (methodCall.Type == typeof(void))
                {
                    var lambda = Expression.Lambda<VoidMethodExecutor>(methodCall, targetParameter, parametersParameter);
                    var voidExecutor = lambda.Compile();
                    return WrapVoidAction(voidExecutor);
                }
                else
                {
                    // must coerce methodCall to match ActionExecutor signature
                    var castMethodCall = Expression.Convert(methodCall, typeof(object));
                    var lambda = Expression.Lambda<MethodExecutor>(castMethodCall, targetParameter, parametersParameter);
                    return lambda.Compile();
                }
            }

            private static MethodExecutor WrapVoidAction(VoidMethodExecutor executor)
            {
                return delegate(object target, object[] parameters)
                {
                    executor(target, parameters);
                    return null;
                };
            }

            #region Nested type: MethodExecutor

            private delegate object MethodExecutor(object taget, object[] parameters);

            #endregion

            #region Nested type: VoidMethodExecutor

            private delegate void VoidMethodExecutor(object target, object[] parameters);

            #endregion
        }

        #endregion
    }

}
