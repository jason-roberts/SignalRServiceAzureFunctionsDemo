using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Extensions.Logging;

namespace DontCodeTiredDemosV2.SignalRDemo
{

    public class OrderPlacement
    {
        public string CustomerName { get; set; }
        public string Product { get; set; }
    }


    // Simplified for demo purposes
    public class Order
    {
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public string CustomerName { get; set; }
        public string Product { get; set; }
    }


    public static class PlacedOrderNotifications
    {
        public static class PlaceOrder
        {
            [FunctionName("PlaceOrder")]
            public static async Task<IActionResult> Run(
                [HttpTrigger(AuthorizationLevel.Function, "post")] OrderPlacement orderPlacement,
                [Table("Orders")] IAsyncCollector<Order> orders, // could use cosmos db etc.
                [Queue("new-order-notifications")] IAsyncCollector<OrderPlacement> notifications,
                ILogger log)
            {
                await orders.AddAsync(new Order
                {
                    PartitionKey = "US",
                    RowKey = Guid.NewGuid().ToString(),
                    CustomerName = orderPlacement.CustomerName,
                    Product = orderPlacement.Product
                });

                await notifications.AddAsync(orderPlacement);

                return new OkResult();
            }
        }



        // sig r stuff:

        [FunctionName("negotiate")]
        public static SignalRConnectionInfo GetOrderNotificationsSignalRInfo(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
            [SignalRConnectionInfo(HubName = "notifications")] SignalRConnectionInfo connectionInfo)
        {
            return connectionInfo;
        }

        [FunctionName("PlacedOrderNotification")]
        public static async Task Run(
            [QueueTrigger("new-order-notifications")] OrderPlacement orderPlacement,
            [SignalR(HubName = "notifications")] IAsyncCollector<SignalRMessage> signalRMessages,
            ILogger log)
        {
            log.LogInformation($"Sending notification for {orderPlacement.CustomerName}");

            await signalRMessages.AddAsync(
                new SignalRMessage
                {
                    Target = "productOrdered",
                    Arguments = new[] { orderPlacement }
                });
        }
    }

}
