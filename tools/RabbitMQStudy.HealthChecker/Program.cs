using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

Console.WriteLine("🏥 Health Checker iniciado");

// Carregar configuração
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var rabbitMQHost = configuration["RabbitMQ:Host"] ?? "localhost";
Console.WriteLine($"Conectando ao RabbitMQ em: {rabbitMQHost}");

var factory = new ConnectionFactory() { HostName = rabbitMQHost };

while (true)
{
    try
    {
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        var mainQueue = await channel.QueueDeclarePassiveAsync("order-processor");
        var dlqQueue = await channel.QueueDeclarePassiveAsync("order-processor.dead");

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Status:");
        Console.WriteLine($"  📥 Fila principal: {mainQueue.MessageCount} mensagens");
        Console.WriteLine($"  💀 DLQ: {dlqQueue.MessageCount} mensagens");

        if (dlqQueue.MessageCount > 10)
        {
            Console.WriteLine("⚠️  ALERTA: Muitas mensagens na DLQ!");
            // Local para enviar notificação, email, etc.
        }

        Console.WriteLine($"✅ Sistema saudável");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Erro de conectividade: {ex.Message}");
    }

    await Task.Delay(30000);
}