using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQStudy.Domain.Events;
using RabbitMQStudy.Infrastructure.Messaging;

Console.WriteLine("📧 Email Service");
Console.WriteLine("Aguardando mensagens...");

var rabbitMqHost = Environment.GetEnvironmentVariable("RabbitMQ__Host") ?? "localhost";
var factory = new ConnectionFactory() { HostName = rabbitMqHost };
using var connection = await factory.CreateConnectionAsync();
using var channel = await connection.CreateChannelAsync();

// Declarar exchange e DLX
await channel.ExchangeDeclareAsync("orders", ExchangeType.Topic, true);
await channel.ExchangeDeclareAsync("orders.dlx", ExchangeType.Direct, true);

// Declarar a fila principal com configuração de DLQ
await channel.QueueDeclareAsync(
    queue: "email-service",
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: new Dictionary<string, object?>
    {
        { "x-dead-letter-exchange", "orders.dlx" },
        { "x-dead-letter-routing-key", "email-service.dead" }
    });

// Bind da fila principal
await channel.QueueBindAsync("email-service", "orders", "order.created");

// Declarar a DLQ
await channel.QueueDeclareAsync(
    queue: "email-service.dead",
    durable: true,
    exclusive: false,
    autoDelete: false);

// Bind da DLQ
await channel.QueueBindAsync("email-service.dead", "orders.dlx", "email-service.dead");

// Configurar retry
var retryPolicy = new OrderConsumerRetryPolicy(channel, "email-service");

// Configurar consumer
var consumer = new AsyncEventingBasicConsumer(channel);
consumer.ReceivedAsync += async (model, ea) =>
{
    var retryCount = OrderConsumerRetryPolicy.GetRetryCount(ea.BasicProperties);

    try
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(message);

        if (orderEvent == null)
        {
            throw new Exception("Falha ao deserializar a mensagem");
        }

        Console.WriteLine($"🔄 Tentativa {retryCount + 1} - Enviando email para pedido: {orderEvent.OrderId}");

        // Simular falha ocasional para testar retry
        if (Random.Shared.Next(1, 5) == 1) // 20% chance de falha
        {
            throw new Exception("Falha simulada no envio de email");
        }

        await SendEmailAsync(orderEvent);

        Console.WriteLine($"✅ Email enviado com sucesso para o pedido {orderEvent.OrderId}!");
        await channel.BasicAckAsync(ea.DeliveryTag, false);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Erro ao enviar email: {ex.Message}");
        await retryPolicy.HandleFailureAsync(ea, retryCount, ex);
    }
};

// Configurar prefetch count
await channel.BasicQosAsync(0, 1, false);

await channel.BasicConsumeAsync("email-service", false, consumer);

Console.WriteLine("Serviço em execução. Aguardando mensagens...");

// Manter o aplicativo em execução
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => {
    e.Cancel = true;
    cts.Cancel();
};

try
{
    await Task.Delay(-1, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Serviço finalizado.");
}

// Método para simular envio de email
static async Task SendEmailAsync(OrderCreatedEvent order)
{
    // Simulação de envio de email
    Console.WriteLine($"📤 Enviando email para {order.CustomerEmail}:");
    Console.WriteLine($"   Assunto: Seu pedido #{order.OrderId} foi recebido");
    Console.WriteLine($"   Conteúdo: Olá {order.CustomerName}, seu pedido no valor de R${order.TotalAmount:F2} foi recebido e está sendo processado.");

    // Simular processamento
    await Task.Delay(500);
}