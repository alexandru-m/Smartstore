﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Checkout.Cart;

namespace Smartstore.Core.Checkout.Orders.Requirements
{
    public abstract class CheckoutRequirementBase : ICheckoutRequirement
    {
        protected readonly IHttpContextAccessor _httpContextAccessor;

        protected CheckoutRequirementBase(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public abstract int Order { get; }

        protected abstract RedirectToActionResult FulfillResult { get; }

        public virtual Task<bool> IsFulfilledAsync(ShoppingCart cart, object model = null)
            => Task.FromResult(false);

        public virtual IActionResult Fulfill()
        {
            var request = _httpContextAccessor.HttpContext.Request;
            var result = FulfillResult;

            if (request.Method.EqualsNoCase(HttpMethods.Get) &&
                request.RouteValues.IsSameRoute(result.RouteValues.GetAreaName(), result.ControllerName, result.ActionName))
            {
                // Current route. Do not redirect to self.
                return null;
            }

            return result;
        }

        protected bool IsSameRoute(string method, string action, string controller = "Checkout", string area = null)
        {
            var request = _httpContextAccessor.HttpContext.Request;
            return request.Method.EqualsNoCase(method) && request.RouteValues.IsSameRoute(area, controller, action);
        }
    }
}
