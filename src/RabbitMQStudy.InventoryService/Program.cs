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

// Declarar exchange e filas
await channel.ExchangeDeclareAsync("orders", ExchangeType.Topic, true);
await channel.QueueDeclareAsync("inventory-service", true, false, false);
await channel.QueueBindAsync("inventory-service", "orders", "order.created");

// Configurar DLX e DLQ
await channel.ExchangeDeclareAsync("orders.dlx", ExchangeType.Direct, true);
await channel.QueueDeclareAsync("inventory-service.dead", true, false, false);
await channel.QueueBindAsync("inventory-service.dead", "orders.dlx", "inventory-service");

// Configurar retry
var retryPolicy = new OrderConsumerRetryPolicy(channel, "inventory-service");

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

        Console.WriteLine($"🔄 Tentativa {retryCount + 1} - Atualizando estoque para pedido: {orderEvent.OrderId}");

        // Simular falha ocasional para testar retry
        if (Random.Shared.Next(1, 6) == 1) // ~17% chance de falha
        {
            throw new Exception("Falha simulada na atualização de estoque");
        }

        await UpdateInventoryAsync(orderEvent);

        Console.WriteLine($"✅ Estoque atualizado com sucesso para o pedido {orderEvent.OrderId}!");
        await channel.BasicAckAsync(ea.DeliveryTag, false);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Erro ao atualizar estoque: {ex.Message}");
        await retryPolicy.HandleFailureAsync(ea, retryCount, ex);
    }
};

await channel.BasicConsumeAsync("inventory-service", false, consumer);

Console.WriteLine("Serviço em execução. Aguardando mensagens...");

// Manter o aplicativo em execução
await Task.Delay(Timeout.Infinite);

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