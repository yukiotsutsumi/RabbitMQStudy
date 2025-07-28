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

            // Configurar filas de serviços
            await ConfigureServiceQueueAsync(channel, "order-processor");
            await ConfigureServiceQueueAsync(channel, "email-service");
            await ConfigureServiceQueueAsync(channel, "inventory-service");
        }

        private static async Task ConfigureServiceQueueAsync(IChannel channel, string serviceName)
        {
            // Fila principal com configuração de DLQ
            var queueArgs = new Dictionary<string, object?>
            {
                {"x-dead-letter-exchange", "orders.dlx"},
                {"x-dead-letter-routing-key", $"{serviceName}.dead"}
            };

            // Declarar fila principal
            await channel.QueueDeclareAsync(
                queue: serviceName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: queueArgs);

            // Bind da fila principal à exchange
            await channel.QueueBindAsync(serviceName, "orders", "order.created");

            // Declarar fila DLQ
            await channel.QueueDeclareAsync(
                queue: $"{serviceName}.dead",
                durable: true,
                exclusive: false,
                autoDelete: false);

            // Bind da fila DLQ à exchange DLX
            await channel.QueueBindAsync($"{serviceName}.dead", "orders.dlx", $"{serviceName}.dead");
        }
    }
}