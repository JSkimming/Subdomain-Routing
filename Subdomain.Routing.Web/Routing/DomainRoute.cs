using System;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Subdomain.Routing.Routing
{
    public class DomainRoute : Route
    {
        public string Domain { get; private set; }

        private readonly Regex _tokenRegex =
            new Regex(@"({[a-zA-Z0-9_]*})*-?\.?\/?({[a-zA-Z0-9_]*})*-?\.?\/?({[a-zA-Z0-9_]*})*-?\.?\/?({[a-zA-Z0-9_]*})*-?\.?\/?({[a-zA-Z0-9_]*})*-?\.?\/?({[a-zA-Z0-9_]*})*-?\.?\/?({[a-zA-Z0-9_]*})*-?\.?\/?({[a-zA-Z0-9_]*})*-?\.?\/?({[a-zA-Z0-9_]*})*-?\.?\/?({[a-zA-Z0-9_]*})*-?\.?\/?({[a-zA-Z0-9_]*})*-?\.?\/?({[a-zA-Z0-9_]*})*-?\.?\/?");

        private readonly Regex _domainRegex;

        public DomainRoute(string domain, string url, RouteValueDictionary defaults)
            : this(domain, url, defaults, new MvcRouteHandler())
        {
        }

        public DomainRoute(string domain, string url, object defaults)
            : this(domain, url, new RouteValueDictionary(defaults), new MvcRouteHandler())
        {
        }

        public DomainRoute(string domain, string url, object defaults, IRouteHandler routeHandler)
            : this(domain, url, new RouteValueDictionary(defaults), routeHandler)
        {
        }

        public DomainRoute(string domain, string url, RouteValueDictionary defaults, IRouteHandler routeHandler)
            : base(url, defaults, routeHandler)
        {
            Domain = domain;
            _domainRegex = CreateRegex(Domain);
        }

        public override RouteData GetRouteData(HttpContextBase httpContext)
        {
            // Request information
            string requestDomain = httpContext.Request.Headers["host"];
            if (!string.IsNullOrWhiteSpace(requestDomain))
            {
                if (requestDomain.IndexOf(":", StringComparison.Ordinal) > 0)
                {
                    requestDomain = requestDomain.Substring(0, requestDomain.IndexOf(":", StringComparison.Ordinal));
                }
            }
            else
            {
                if (httpContext.Request.Url == null) return null;
                requestDomain = httpContext.Request.Url.Host;
            }

            // Match domain and route
            Match domainMatch = _domainRegex.Match(requestDomain);
            if (domainMatch.Success == false) return null;

            RouteData data = base.GetRouteData(httpContext);
            if (data == null) return null;

            // Iterate matching domain groups
            for (int i = 1; i < domainMatch.Groups.Count; i++)
            {
                Group group = domainMatch.Groups[i];
                if (group.Success)
                {
                    string key = _domainRegex.GroupNameFromNumber(i);

                    if (!string.IsNullOrEmpty(key) && !char.IsNumber(key, 0))
                    {
                        if (!string.IsNullOrEmpty(group.Value))
                        {
                            data.Values[key] = group.Value;
                        }
                    }
                }
            }

            return data;
        }

        public override VirtualPathData GetVirtualPath(RequestContext requestContext, RouteValueDictionary values)
        {
            return base.GetVirtualPath(requestContext, RemoveDomainTokens(values));
        }

        private Regex CreateRegex(string source)
        {
            // Perform replacements
            source = source.Replace("/", @"\/?");
            source = source.Replace(".", @"\.?");
            source = source.Replace("-", @"\-?");
            source = source.Replace("{", @"(?<");
            source = source.Replace("}", @">([a-zA-Z0-9_]*))");

            return new Regex("^" + source + "$");
        }

        private RouteValueDictionary RemoveDomainTokens(RouteValueDictionary values)
        {
            Match tokenMatch = _tokenRegex.Match(Domain);
            for (int i = 0; i < tokenMatch.Groups.Count; i++)
            {
                Group group = tokenMatch.Groups[i];
                if (group.Success)
                {
                    string key = group.Value.Replace("{", "").Replace("}", "");
                    if (values.ContainsKey(key))
                        values.Remove(key);
                }
            }

            return values;
        }
    }
}
