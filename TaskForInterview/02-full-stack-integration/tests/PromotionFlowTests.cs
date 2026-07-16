using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using Npgsql;
using System.Net.Http;
using System.Text.Json;
using EcommerceTests.Helpers;
using EcommerceTests.PageObjects;
using Microsoft.Extensions.Configuration;

namespace EcommerceTests.Integration
{
    [TestFixture]
    public class PromotionFlowTests : PageTest
    {
        private ApiClient _apiClient;
        private DatabaseHelper _dbHelper;
        private string _testPromotionId;
        private IConfiguration _config;
        private HttpClient _resetClient;
        private string _lastOrderId;

        [SetUp]
            public async Task Setup()
            {
                _config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

                var apiBaseUrl = _config["TestConfiguration:Api:BaseUrl"];
                var dbHost = _config["TestConfiguration:Database:Host"];
                var dbPort = int.Parse(_config["TestConfiguration:Database:Port"]);
                var dbName = _config["TestConfiguration:Database:Database"];
                var dbUser = _config["TestConfiguration:Database:Username"];
                var dbPassword = _config["TestConfiguration:Database:Password"];

                _apiClient = new ApiClient(apiBaseUrl);
                _dbHelper = new DatabaseHelper(dbHost,dbPort,dbName,dbUser,dbPassword);
                _dbHelper.Connect();

                _resetClient = new HttpClient { BaseAddress = new Uri(apiBaseUrl)};
                await _resetClient.PostAsync("/admin/reset", null);

            }

        [TearDown]
        public async Task Cleanup()
        {
            if (!string.IsNullOrEmpty(_testPromotionId))
            {
                try 
                {
                    await _apiClient.DeletePromotionAsync(_testPromotionId);
            
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(_lastOrderId))
            {
                try
                {
                    _dbHelper.DeleteOrder(_lastOrderId);


                }
                catch { }
            }

            _dbHelper?.Disconnect();
            await _resetClient.PostAsync("/admin/reset", null);


        }

        [Test]
        public async Task TestFullPromotionFlowHappyPath()
        {
            var validFrom = DateTime.UtcNow.AddDays(-1);
            var validUntil = DateTime.UtcNow.AddDays(30);

            var promotionData = new 
            {
                code = "SPRING25",
                discountType = "PERCENTAGE",
                discountValue = 25,
                category = "ELECTRONICS",
                maxUses = 100,
                validFrom = validFrom.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                validUntil = validUntil.ToString("yyyy-MM-ddTHH:mm:ssZ")


            };

            var promoResponse = await _apiClient.CreatePromotionAsync(promotionData);
            _testPromotionId = promoResponse.PromotionId;

            Assert.IsNotNull(promoResponse.PromotionId);
            Assert.AreEqual("SPRING25" , promoResponse.Code);
            Assert.AreEqual("ACTIVE", promoResponse.Status);

            var checkoutPage = new CheckoutPage(Page);
            await checkoutPage.NavigateAsync();

            var originalPrice = await checkoutPage.GetOriginalPriceAsync();
            Assert.AreEqual(1000.00m, originalPrice);

            await checkoutPage.ApplyPromoCodeAsync("SPRING25");
            await checkoutPage.VerifyDiscountApplied(250.00m);

            var discountAmount = await checkoutPage.GetDiscountAmountAsync();
            var finalPrice = await checkoutPage.GetFinalPriceAsync();

            Assert.AreEqual(250.00m,discountAmount);
            Assert.AreEqual(750.00m,finalPrice);

            var orderId = await checkoutPage.PlaceOrderAsync();
            _lastOrderId = orderId;

            Assert.IsNotNull(orderId);
            Assert.IsTrue(orderId.StartsWith("ORD-"));

            await Task.Delay(1000);
            var order = _dbHelper.GetOrderById(orderId);
            Assert.IsNotNull(order);
            Assert.AreEqual(1000.00m,order.OriginalAmount);
            Assert.AreEqual(250.00m, order.DiscountAmount);
            Assert.AreEqual(750.00m, order.FinalAmount);
            Assert.AreEqual("SPRING25",order.PromotionCode);
            Assert.AreEqual("COMPLETED",order.Status);

            var auditLog = _dbHelper.GetAuditLogByOrderId(orderId);
            Assert.IsNotNull(auditLog);
            Assert.AreEqual(_testPromotionId, auditLog.PromotionId);
            Assert.AreEqual(250.00m, auditLog.DiscountApplied);

            var totalsValid = _dbHelper.VerifyOrderTotals(orderId, 1000.00m, 250.00m, 750.00m);
            Assert.IsTrue(totalsValid);

        }

        [Test]
        public async Task TestInvalidPromoCode()
        {
            var checkoutPage = new CheckoutPage(Page);
            await checkoutPage.NavigateAsync();
            await checkoutPage.ApplyPromoCodeAsync("INVALIDCODE");

            var idErrorDisplayed = await checkoutPage.IsErrorDisplayedAsync();
            Assert.IsTrue(idErrorDisplayed);

            var errorMessage = await checkoutPage.GetErrorMessageAsync();
            Assert.IsTrue(errorMessage.Contains("Invalid") || errorMessage.Contains("not found") || errorMessage.Contains("Promotion not found"));

            var finalPrice = await checkoutPage.GetFinalPriceAsync();
            Assert.AreEqual(1000.00m,finalPrice);

        }

        [Test]
        public async Task TestExpiredPromoCode()
        {
            var validFrom = DateTime.UtcNow.AddDays(-30);
            var validUntil = DateTime.UtcNow.AddDays(-1);
            var promotionData = new 
            {
                code = "EXPIRED20",
                discountType = "PERCENTAGE",
                discountValue = 20,
                category = "ELECTRONICS",
                maxUses = 100,
                validFrom = validFrom.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                validUntil = validUntil.ToString("yyyy-MM-ddTHH:mm:ssZ")

            };
            var promoResponse = await _apiClient.CreatePromotionAsync(promotionData);
            _testPromotionId = promoResponse.PromotionId;

            var checkoutPage = new CheckoutPage(Page);
            await checkoutPage.NavigateAsync();

            await checkoutPage.ApplyPromoCodeAsync("EXPIRED20");

            var isErrorDisplayed = await checkoutPage.IsErrorDisplayedAsync();
            Assert.IsTrue(isErrorDisplayed);

            var errorMessage = await checkoutPage.GetErrorMessageAsync();
            Assert.IsTrue(errorMessage.Contains("expired") || errorMessage.Contains("not yet valid"));

            var finalPrice = await checkoutPage.GetFinalPriceAsync();
            Assert.AreEqual(1000.00m, finalPrice);

        }

        [Test]
        public async Task TestWrongCategoryPromo()
        {
            var validFrom = DateTime.UtcNow.AddDays(-1);
            var validUntil = DateTime.UtcNow.AddDays(30);

            var promotionData = new 
            {
                code = "BOOKS15",
                discountType = "PERCENTAGE",
                discountValue = 15,
                category = "BOOKS",
                maxUses = 100,
                validFrom = validFrom.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                validUntil = validUntil.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            var promoResponse = await _apiClient.CreatePromotionAsync(promotionData);
            _testPromotionId = promoResponse.PromotionId;

            var checkoutPage = new CheckoutPage(Page);
            await checkoutPage.NavigateAsync();

            await checkoutPage.ApplyPromoCodeAsync("BOOKS15");

            var isErrorDisplayed = await checkoutPage.IsErrorDisplayedAsync();
            Assert.IsTrue(isErrorDisplayed);

            var errorMessage = await checkoutPage.GetErrorMessageAsync();
            Assert.IsTrue(errorMessage.Contains("not valid for") || errorMessage.Contains("ELECTRONICS"));

            var finalPrice = await checkoutPage.GetFinalPriceAsync();
            Assert.AreEqual(1000.00m, finalPrice);

        }
    }   
}

