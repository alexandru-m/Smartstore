﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Checkout.Shipping;

namespace Smartstore.Core.Checkout.Orders.Requirements
{
    public class ShippingMethodRequirement : CheckoutRequirementBase
    {
        private readonly IShippingService _shippingService;
        private readonly ICheckoutStateAccessor _checkoutStateAccessor;
        private readonly ShippingSettings _shippingSettings;

        public ShippingMethodRequirement(
            IShippingService shippingService,
            IHttpContextAccessor httpContextAccessor,
            ICheckoutStateAccessor checkoutStateAccessor,
            ShippingSettings shippingSettings)
            : base(httpContextAccessor)
        {
            _shippingService = shippingService;
            _checkoutStateAccessor = checkoutStateAccessor;
            _shippingSettings = shippingSettings;
        }

        public override int Order => 30;

        protected override RedirectToActionResult FulfillResult
            => CheckoutWorkflow.RedirectToCheckout("ShippingMethod");

        public override async Task<bool> IsFulfilledAsync(ShoppingCart cart, object model = null)
        {
            var customer = cart.Customer;
            var attributes = customer.GenericAttributes;

            if (model != null
                && model is string shippingOption 
                && IsSameRoute(HttpMethods.Post, "SelectShippingMethod"))
            {
                var splittedOption = shippingOption.SplitSafe("___").ToArray();
                if (splittedOption.Length == 2)
                {
                    var selectedName = splittedOption[0];
                    var providerSystemName = splittedOption[1];
                    var shippingOptions = attributes.OfferedShippingOptions;

                    if (shippingOptions.IsNullOrEmpty())
                    {
                        // Shipping option was not found in customer attributes. Load via shipping service.
                        shippingOptions = await GetShippingOptions(cart, providerSystemName);
                    }
                    else
                    {
                        // Loaded cached results. Filter result by a chosen shipping rate computation method.
                        shippingOptions = shippingOptions.Where(x => x.ShippingRateComputationMethodSystemName.EqualsNoCase(providerSystemName)).ToList();
                    }

                    var selectedShippingOption = shippingOptions.Find(x => x.Name.EqualsNoCase(selectedName));
                    if (selectedShippingOption != null)
                    {
                        // Save selected shipping option in customer attributes.
                        attributes.SelectedShippingOption = selectedShippingOption;
                        await attributes.SaveChangesAsync();

                        return true;
                    }
                }

                return false;
            }

            if (!cart.IsShippingRequired())
            {
                if (attributes.SelectedShippingOption != null || attributes.OfferedShippingOptions != null)
                {
                    attributes.SelectedShippingOption = null;
                    attributes.OfferedShippingOptions = null;
                    await attributes.SaveChangesAsync();
                }

                return true;
            }

            if (attributes.SelectedShippingOption != null)
            {
                return true;
            }

            var saveAttributes = false;
            var options = attributes.OfferedShippingOptions;

            if (options == null)
            {
                options = await GetShippingOptions(cart);

                // TODO: (mg)(quick-checkout) CheckoutShippingMethodMapper: updating customer.GenericAttributes.OfferedShippingOptions is redundant. Done by this requirement.
                // TODO: (mg)(quick-checkout) CheckoutShippingMethodMapper: use customer.GenericAttributes.OfferedShippingOptions instead of "ShippingOptionResponse" parameter.

                // Performance optimization. Cache returned shipping options.
                // We will use them later (after a customer has selected an option).
                attributes.OfferedShippingOptions = options;
                saveAttributes = true;
            }

            if (_shippingSettings.SkipShippingIfSingleOption && options.Count == 1)
            {
                attributes.SelectedShippingOption = options[0];
                saveAttributes = true;
            }

            if (saveAttributes)
            {
                await attributes.SaveChangesAsync();
            }

            // TODO: (mg)(quick-checkout) "HasOnlyOneActiveShippingMethod" is redundant. Instead use customer.GenericAttributes.OfferedShippingOptions.Count == 1
            _checkoutStateAccessor.CheckoutState.CustomProperties["HasOnlyOneActiveShippingMethod"] = options.Count == 1;

            return attributes.SelectedShippingOption != null;
        }

        private async Task<List<ShippingOption>> GetShippingOptions(ShoppingCart cart, string providerSystemName = null)
        {
            return (await _shippingService.GetShippingOptionsAsync(cart, cart.Customer.ShippingAddress, providerSystemName, cart.StoreId)).ShippingOptions;
        }
    }
}
