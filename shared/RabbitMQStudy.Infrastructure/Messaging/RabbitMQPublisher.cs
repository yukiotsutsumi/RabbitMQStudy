using RabbitMQ.Client;
using RabbitMQStudy.Application.Interfaces;
using System.Text;
using System.Text.Json;

namespace RabbitMQStudy.Infrastructure.Messaging
{
    public class RabbitMQPublisher : IMessagePublisher, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IChannel channel;
        private bool _disposed = false;

        public RabbitMQPublisher()
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

            channel.ExchangeDeclareAsync("orders", ExchangeType.Topic, durable: true).GetAwaiter().GetResult();
        }

        public async Task PublishAsync<T>(string routingKey, T message)
        {
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            await channel.BasicPublishAsync(
                exchange: "orders",
                routingKey: routingKey,
                body: body);

            Console.WriteLine($"Mensagem publicada: {routingKey}");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    channel?.Dispose();
                    _connection?.Dispose();
                }

                _disposed = true;
            }
        }
    }
}