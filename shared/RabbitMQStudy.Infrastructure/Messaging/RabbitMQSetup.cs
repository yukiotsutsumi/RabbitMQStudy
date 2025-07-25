using RabbitMQ.Client;

namespace RabbitMQStudy.Infrastructure.Messaging
{
    public class RabbitMQSetup
    {
        public static async Task ConfigureAsync(IChannel channel)
        {
            // Exchange principal
            await channel.ExchangeDeclareAsync("orders", ExchangeType.Topic, durable: true);

            // Dead Letter Exchange
            await channel.ExchangeDeclareAsync("orders.dlx", ExchangeType.Direct, durable: true);

            var queueArgs = new Dictionary<string, object?>
            {
                {"x-dead-letter-exchange", "orders.dlx"},
                {"x-dead-letter-routing-key", "failed"},
                {"x-message-ttl", 300000},
                {"x-max-retries", 3}
            };

            await channel.QueueDeclareAsync("order-processor", durable: true, exclusive: false, autoDelete: false, arguments: queueArgs);
            await channel.QueueBindAsync("order-processor", "orders", "order.created");

            // Dead Letter Queue
            await channel.QueueDeclareAsync("order-processor.dead", durable: true, exclusive: false, autoDelete: false);
            await channel.QueueBindAsync("order-processor.dead", "orders.dlx", "failed");

            // Retry Queue (para reprocessamento manual)
            await channel.QueueDeclareAsync("order-processor.retry", durable: true, exclusive: false, autoDelete: false);
        }
    }
}