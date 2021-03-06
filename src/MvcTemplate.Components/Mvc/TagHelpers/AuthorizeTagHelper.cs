﻿using Microsoft.AspNet.Mvc.Rendering;
using Microsoft.AspNet.Mvc.ViewFeatures;
using Microsoft.AspNet.Razor.TagHelpers;
using MvcTemplate.Components.Security;
using System;

namespace MvcTemplate.Components.Mvc
{
    [HtmlTargetElement("authorize", Attributes = "area")]
    [HtmlTargetElement("authorize", Attributes = "action")]
    [HtmlTargetElement("authorize", Attributes = "controller")]
    public class AuthorizeTagHelper : TagHelper
    {
        public String Area { get; set; }
        public String Action { get; set; }
        public String Controller { get; set; }

        [ViewContext]
        [HtmlAttributeNotBound]
        public ViewContext ViewContext { get; set; }

        private IAuthorizationProvider AuthorizationProvider { get; }

        public AuthorizeTagHelper(IAuthorizationProvider provider)
        {
            AuthorizationProvider = provider;
        }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = null;
            if (AuthorizationProvider == null) return;

            String accountId = ViewContext.HttpContext.User.Identity.Name;
            String area = Area ?? ViewContext.RouteData.Values["area"] as String;
            String action = Action ?? ViewContext.RouteData.Values["action"] as String;
            String controller = Controller ?? ViewContext.RouteData.Values["controller"] as String;

            if (!AuthorizationProvider.IsAuthorizedFor(accountId, area, controller, action))
                output.SuppressOutput();
        }
    }
}
