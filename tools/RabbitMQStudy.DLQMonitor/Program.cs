using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

Console.WriteLine("🔍 DLQ Monitor - Pressione Ctrl+C para sair");

var factory = new ConnectionFactory() { HostName = "localhost" };
using var connection = await factory.CreateConnectionAsync();
using var channel = await connection.CreateChannelAsync();

var consumer = new AsyncEventingBasicConsumer(channel);
consumer.ReceivedAsync += async (model, ea) =>
{
    var body = ea.Body.ToArray();
    var message = Encoding.UTF8.GetString(body);
    var headers = ea.BasicProperties.Headers;

    Console.WriteLine($"💀 [{DateTime.Now:HH:mm:ss}] Dead Letter detectada:");
    Console.WriteLine($"   📄 Mensagem: {message}");
    Console.WriteLine($"   ❌ Motivo: {headers?["x-death-reason"]}");
    Console.WriteLine($"   🔄 Tentativas: {headers?["x-retry-count"]}");
    if (headers?.TryGetValue("x-death-timestamp", out var timestamp) == true && timestamp != null)
    {
        Console.WriteLine($"   ⏰ Timestamp: {DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(timestamp))}");
    }
    else
    {
        Console.WriteLine($"   ⏰ Timestamp: N/A");
    }
    Console.WriteLine();

    await channel.BasicAckAsync(ea.DeliveryTag, false);
};

await channel.BasicConsumeAsync("order-processor.dead", false, consumer);

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
    Console.WriteLine("🛑 DLQ Monitor finalizado.");
}