using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMQStudy.Infrastructure.Messaging;

public class OrderConsumerRetryPolicy(IChannel channel, string queueName, int maxRetries = 3)
{
    private readonly IChannel _channel = channel;

    public static int GetRetryCount(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers != null &&
            properties.Headers.TryGetValue("x-retry-count", out var retryCountObj) &&
            retryCountObj != null)
        {
            return Convert.ToInt32(retryCountObj);
        }

        return 0;
    }

    public async Task HandleFailureAsync(BasicDeliverEventArgs ea, int currentRetryCount, Exception exception)
    {
        if (currentRetryCount < maxRetries)
        {
            // Incrementar contador de retry
            var nextRetryCount = currentRetryCount + 1;

            // Calcular delay com backoff exponencial
            var delayMs = (int)Math.Pow(2, nextRetryCount) * 1000; // 2s, 4s, 8s...

            // Adicionar jitter (variação aleatória) para evitar thundering herd
            delayMs += Random.Shared.Next(100, 1000);

            Console.WriteLine($"⏱️ Agendando retry #{nextRetryCount} em {delayMs}ms");

            // Criar fila de delay temporária
            var delayQueueName = $"{queueName}.delay.{Guid.NewGuid()}";

            await _channel.QueueDeclareAsync(
                delayQueueName,
                durable: true,
                exclusive: false,
                autoDelete: true,
                arguments: new Dictionary<string, object?>
                {
                    { "x-dead-letter-exchange", "orders" },
                    { "x-dead-letter-routing-key", "order.created" },
                    { "x-message-ttl", delayMs }
                });

            // Criar propriedades com contador de retry
            var properties = new BasicProperties
            {
                Headers = new Dictionary<string, object?>
                {
                    { "x-retry-count", nextRetryCount },
                    { "x-exception", exception.Message },
                    { "x-failed-at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
                }
            };

            // Publicar na fila de delay
            await _channel.BasicPublishAsync("", delayQueueName, false, properties, ea.Body);

            // Acknowledge a mensagem original
            await _channel.BasicAckAsync(ea.DeliveryTag, false);
        }
        else
        {
            Console.WriteLine($"❌ Número máximo de retentativas atingido. Enviando para DLQ.");

            // Criar propriedades para DLQ
            var properties = new BasicProperties
            {
                Headers = new Dictionary<string, object?>
                {
                    { "x-exception", exception.Message },
                    { "x-failed-at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                    { "x-retry-count", currentRetryCount },
                    { "x-original-exchange", "orders" },
                    { "x-original-routing-key", "order.created" }
                }
            };

            // Publicar na DLQ
            await _channel.BasicPublishAsync("orders.dlx", queueName, false, properties, ea.Body);

            // Acknowledge a mensagem original
            await _channel.BasicAckAsync(ea.DeliveryTag, false);
        }
    }
}