using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQStudy.Application.Interfaces;
using System.Text;
using System.Text.Json;

namespace RabbitMQStudy.Infrastructure.Messaging
{
    public class RabbitMQPublisher : IMessagePublisher, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IChannel _channel;
        private readonly ILogger<RabbitMQPublisher> _logger;
        private bool _disposed = false;

        public RabbitMQPublisher(IOptions<RabbitMQSettings> options, ILogger<RabbitMQPublisher> logger)
        {
            _logger = logger;
            var settings = options.Value;

            _logger.LogInformation("Inicializando RabbitMQPublisher com host: {Host}", settings.Host);

            try
            {
                Console.WriteLine($"RabbitMQPublisher - Connecting to: {settings.Host}:{settings.Port}");
                logger.LogInformation("RabbitMQPublisher - Connecting to: {Host}:{Port}", settings.Host, settings.Port);

                var factory = new ConnectionFactory()
                {
                    HostName = settings.Host,
                    Port = settings.Port,
                    UserName = settings.UserName,
                    Password = settings.Password,
                    VirtualHost = settings.VirtualHost ?? "/"
                };

                // Log explícito da factory  
                Console.WriteLine($"ConnectionFactory configured with: {factory.HostName}:{factory.Port}");

                _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
                _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

                _channel.ExchangeDeclareAsync("orders", ExchangeType.Topic, durable: true).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao inicializar RabbitMQPublisher: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        public async Task PublishAsync<T>(string routingKey, T message)
        {
            try
            {
                _logger.LogInformation("Publicando mensagem com routing key: {RoutingKey}", routingKey);

                var json = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(json);

                _logger.LogDebug("Mensagem serializada: {MessageJson}", json);

                await _channel.BasicPublishAsync(
                    exchange: "orders",
                    routingKey: routingKey,
                    body: body);

                _logger.LogInformation("Mensagem publicada com sucesso: {RoutingKey}", routingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao publicar mensagem: {ErrorMessage}", ex.Message);
                throw;
            }
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
                    _logger.LogInformation("Disposing RabbitMQPublisher...");
                    _channel?.Dispose();
                    _connection?.Dispose();
                }

                _disposed = true;
            }
        }
    }
}