using Microsoft.Playwright;

namespace EcommerceTests.PageObjects
{
    public class CheckoutPage
    {
        private readonly IPage _page;

        private ILocator PromoCodeInput => _page.Locator("#promo-code");
        private ILocator ApplyPromoButton => _page.Locator("#apply-promo");
        private ILocator OriginalPrice => _page.Locator(".original-price");
        private ILocator DiscountAmount => _page.Locator(".discount-amount");
        private ILocator FinalPrice => _page.Locator(".final-price");
        private ILocator PlaceOrderButton => _page.Locator("#place-order");
        private ILocator OrderNumber => _page.Locator(".order-number");
        private ILocator SuccessMessage => _page.Locator(".success-message");
        private ILocator ErrorMessage => _page.Locator(".error-message");

        public CheckoutPage(IPage page)
        {
            _page = page;
        }

        public async Task NavigateAsync()
        {
            await _page.GotoAsync("http://localhost:8080");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        public async Task ApplyPromoCodeAsync(string code)
        {
            await PromoCodeInput.FillAsync(code);
            await ApplyPromoButton.ClickAsync();
            await _page.WaitForTimeoutAsync(500);
        }

        public async Task<decimal> GetOriginalPriceAsync()
        {
            var text = await OriginalPrice.TextContentAsync();
            return ParsePrice(text);
        }

        public async Task<decimal> GetDiscountAmountAsync()
        {
            var text = await DiscountAmount.TextContentAsync();
            return ParsePrice(text);
        }

        public async Task<decimal> GetFinalPriceAsync()
        {
            var text = await FinalPrice.TextContentAsync();
            return ParsePrice(text);
        }

        public async Task VerifyDiscountApplied(decimal expectedDiscount)
        {
            await _page.Locator("#promo-message.success-message").WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 5000
            });

            var actualDiscount = await GetDiscountAmountAsync();
            if (Math.Abs(actualDiscount - expectedDiscount) > 0.01m)
            {
                throw new Exception($"Expected discount {expectedDiscount:F2} but got {actualDiscount:F2}");
            }
        }

        public async Task<string> PlaceOrderAsync()
        {
            await PlaceOrderButton.ClickAsync();
            await OrderNumber.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 10000
            });

            var orderNumberText = Await OrderNumber.TextContentAsync();
            return orderNumberText?.Trim();
        }

        public async Task<bool> IsErrorDisplayedAsync()
        {
            try
            {
                await ErrorMessage.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 3000
                });
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GetErrorMessageAsync()
        {
            var text = await ErrorMessage.TextContentAsync();
            return text?.trim();
        }

        private decimal ParsePrice(string priceText)
        {
            if(string.IsNullOrEmpty(priceText))
            return 0m;

            var cleaned = priceText
                .Replace("$","")
                .Replace(",","")
                .Replace("-","")
                .Trim();

            return decimal.Parse(cleaned);
        }
    }
}
