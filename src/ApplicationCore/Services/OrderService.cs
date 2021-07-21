using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.AppSettings;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Services
{
    public class OrderService : IOrderService
    {
        private readonly IAsyncRepository<Order> _orderRepository;
        private readonly IUriComposer _uriComposer;
        private readonly IAsyncRepository<Basket> _basketRepository;
        private readonly IAsyncRepository<CatalogItem> _itemRepository;
        private readonly AzureFunctions _azureFunctions;

        public OrderService(IAsyncRepository<Basket> basketRepository,
            IAsyncRepository<CatalogItem> itemRepository,
            IAsyncRepository<Order> orderRepository,
            IUriComposer uriComposer,
            IOptions<AzureFunctions> azureFunctions)
        {
            _orderRepository = orderRepository;
            _uriComposer = uriComposer;
            _basketRepository = basketRepository;
            _itemRepository = itemRepository;
            _azureFunctions = azureFunctions.Value;
        }

        public async Task CreateOrderAsync(int basketId, Address shippingAddress)
        {
            var basketSpec = new BasketWithItemsSpecification(basketId);
            var basket = await _basketRepository.FirstOrDefaultAsync(basketSpec);

            Guard.Against.NullBasket(basketId, basket);
            Guard.Against.EmptyBasketOnCheckout(basket.Items);

            var catalogItemsSpecification = new CatalogItemsSpecification(basket.Items.Select(item => item.CatalogItemId).ToArray());
            var catalogItems = await _itemRepository.ListAsync(catalogItemsSpecification);

            var items = basket.Items.Select(basketItem =>
            {
                var catalogItem = catalogItems.First(c => c.Id == basketItem.CatalogItemId);
                var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
                var orderItem = new OrderItem(itemOrdered, basketItem.UnitPrice, basketItem.Quantity);
                return orderItem;
            }).ToList();

            var order = new Order(basket.BuyerId, shippingAddress, items);

            await _orderRepository.AddAsync(order);

            await SendOrderDetailsToAzureFunction(order.Id.ToString(), items.Count);

            await SendDeliveryDetailsToAzureFunction(order.ShipToAddress.ToString(), order.OrderItems.Select(item => item.ItemOrdered.ProductName), order.OrderItems.Sum(item => item.UnitPrice * item.Units));
        }

        private async Task SendOrderDetailsToAzureFunction(string id, int quantity)
        {
            if (!_azureFunctions.UploadOrderDetailsIsEnabled)
            {
                return;
            }

            using var httpClient = new HttpClient();
            using var content = new StringContent(JsonConvert.SerializeObject(new { id, quantity }), Encoding.UTF8, "application/json");
            await httpClient.PostAsync(_azureFunctions.UploadOrderDetailsUrl, content);
        }

        private async Task SendDeliveryDetailsToAzureFunction(string shippingAddress, IEnumerable<string> items, decimal finalPrice)
        {
            if (!_azureFunctions.UploadDeliveryDetailsIsEnabled)
            {
                return;
            }

            using var httpClient = new HttpClient();
            using var content = new StringContent(JsonConvert.SerializeObject(new { shippingAddress, items, finalPrice }), Encoding.UTF8, "application/json");
            await httpClient.PostAsync(_azureFunctions.UploadDeliveryDetailsUrl, content);
        }
    }
}
