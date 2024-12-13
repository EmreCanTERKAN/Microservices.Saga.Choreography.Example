using MassTransit;
using MongoDB.Driver;
using Shared;
using Shared.Events;
using Stock.API.Services;

namespace Stock.API.Consumers
{
    public class OrderCreatedEventConsumer(MongoDBService mongoDBService, ISendEndpointProvider sendEndpointProvider, IPublishEndpoint publishEndpoint) : IConsumer<OrderCreatedEvent>
    {
        //readonly MongoDBService _mongoDBService;

        // public OrderCreatedEventConsumer(MongoDBService mongoDBService)
        // {
        //     _mongoDBService = mongoDBService;
        // }

        public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
        {
            List<bool> stockResult = new();
            var collection = mongoDBService.GetCollection<Models.Stock>();

            foreach (var orderItem in context.Message.OrderItems)
            {
                stockResult.Add(await (await collection.FindAsync(s => s.ProductId == orderItem.ProductId.ToString() && s.Count >= orderItem.Count)).AnyAsync());
            }
            if (stockResult.TrueForAll(s => s.Equals(true)))
            {
                //stock güncellemesi..
                foreach (var orderItem in context.Message.OrderItems)
                {
                    Models.Stock stock = await (await collection.FindAsync(s => s.ProductId == orderItem.ProductId.ToString())).FirstOrDefaultAsync();

                    stock.Count -= orderItem.Count;
                    await collection.FindOneAndReplaceAsync(x => x.ProductId == orderItem.ProductId.ToString(), stock);
                }
                //Payment'i uyaracak eventin fırlatırması.

                var sendEndPoint = await sendEndpointProvider.GetSendEndpoint(new Uri($"queue:{RabbitMQSettings.Payment_StockReservedEventQueue}"));
                //yayınlayacağımız eventin instance'ı
                StockReservedEvent stockReservedEvent = new StockReservedEvent()
                {
                    BuyerId = context.Message.BuyerId,
                    OrderId = context.Message.OrderId,
                    TotalPrice = context.Message.TotalPrice,
                    OrderItems = context.Message.OrderItems,
                };

                await sendEndPoint.Send(stockReservedEvent);
            }
            else
            {
                //Stock işlemi başarısız...
                //Orderı uyaracak event fırlatılacaktır..
                StockNotReservedEvent stockNotReservedEvent = new StockNotReservedEvent()
                {
                    BuyerId = context.Message.BuyerId,
                    OrderId = context.Message.OrderId,
                    Message = "Stok miktarı yetersiz..."
                };

                await publishEndpoint.Publish(stockNotReservedEvent);
            }
        }


    }
}
