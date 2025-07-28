using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQStudy.Domain.Events;
using RabbitMQStudy.Infrastructure.Messaging;

Console.WriteLine("📦 Inventory Service");
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
    queue: "inventory-service",
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: new Dictionary<string, object?>
    {
        { "x-dead-letter-exchange", "orders.dlx" },
        { "x-dead-letter-routing-key", "inventory-service.dead" }
    });

// Bind da fila principal
await channel.QueueBindAsync("inventory-service", "orders", "order.created");

// Declarar a DLQ
await channel.QueueDeclareAsync(
    queue: "inventory-service.dead",
    durable: true,
    exclusive: false,
    autoDelete: false);

// Bind da DLQ
await channel.QueueBindAsync("inventory-service.dead", "orders.dlx", "inventory-service.dead");

// Configurar retry
var retryPolicy = new OrderConsumerRetryPolicy(channel, "inventory-service");

// Configurar consumer
var consumer = new AsyncEventingBasicConsumer(channel);
consumer.ReceivedAsync += async (model, ea) =>
{
    try
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(message) ?? throw new Exception("Falha ao deserializar a mensagem");
        Console.WriteLine($"🔄 Tentativa - Atualizando estoque para pedido: {orderEvent.OrderId}");

        // Forçar falha para teste
        //if (orderEvent.CustomerName.Contains("Cliente 1"))
        //{
        //    throw new Exception("Falha forçada para teste de DLQ");
        //}

        await UpdateInventoryAsync(orderEvent);

        Console.WriteLine($"✅ Estoque atualizado com sucesso para o pedido {orderEvent.OrderId}!");
        await channel.BasicAckAsync(ea.DeliveryTag, false);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Erro ao atualizar estoque: {ex.Message}");

        // Extrair o OrderId da mensagem para usar como chave
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(message);
        var messageId = orderEvent?.OrderId.ToString() ?? Guid.NewGuid().ToString();

        await retryPolicy.HandleFailureAsync(ea, messageId, ex);
    }
};

// Configurar prefetch count
await channel.BasicQosAsync(0, 1, false);

await channel.BasicConsumeAsync("inventory-service", false, consumer);

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

// Método para simular atualização de estoque
static async Task UpdateInventoryAsync(OrderCreatedEvent order)
{
    // Simulação de atualização de estoque
    Console.WriteLine($"📝 Atualizando estoque para pedido #{order.OrderId}:");

    foreach (var item in order.Items)
    {
        Console.WriteLine($"   Produto: {item.ProductName} - Quantidade: {item.Quantity}");
        Console.WriteLine($"   Reservando {item.Quantity} unidades no estoque");
    }

    // Simular processamento
    await Task.Delay(700);
}