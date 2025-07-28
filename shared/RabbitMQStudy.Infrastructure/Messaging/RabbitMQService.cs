using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace RabbitMQStudy.Infrastructure.Messaging
{
    public class RabbitMQService
    {
        private readonly RabbitMQSettings _settings;
        private readonly ILogger<RabbitMQService> _logger;

        public RabbitMQService(IOptions<RabbitMQSettings> options, ILogger<RabbitMQService> logger)
        {
            _settings = options.Value;
            _logger = logger;

            _logger.LogInformation("RabbitMQService inicializado com configurações: Host={Host}, Port={Port}",
                _settings.Host, _settings.Port);
        }

        public async Task<IConnection> CreateConnectionAsync()
        {
            try
            {
                _logger.LogInformation("Tentando conectar ao RabbitMQ: {Host}:{Port}",
                    _settings.Host, _settings.Port);

                var factory = new ConnectionFactory
                {
                    HostName = _settings.Host,
                    Port = _settings.Port,
                    UserName = _settings.UserName,
                    Password = _settings.Password,
                    VirtualHost = _settings.VirtualHost,
                    RequestedConnectionTimeout = TimeSpan.FromSeconds(10)
                };

                _logger.LogDebug("Configuração da factory concluída, criando conexão...");
                var connection = await factory.CreateConnectionAsync();
                _logger.LogInformation("Conexão com RabbitMQ estabelecida com sucesso");

                return connection;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao conectar com RabbitMQ: {Message}", ex.Message);
                throw;
            }
        }
    }
}