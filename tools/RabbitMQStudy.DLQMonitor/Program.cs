using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

Console.WriteLine("🔍 DLQ Monitor - Pressione Ctrl+C para sair");

var rabbitMQHost = Environment.GetEnvironmentVariable("RabbitMQ__Host") ?? "localhost";
Console.WriteLine($"Conectando ao RabbitMQ em: {rabbitMQHost}");

var factory = new ConnectionFactory() { HostName = rabbitMQHost };
using var connection = await factory.CreateConnectionAsync();
using var channel = await connection.CreateChannelAsync();

// Lista de filas DLQ para monitorar
var dlqQueues = new[] {
    "order-processor.dead",
    "email-service.dead",
    "inventory-service.dead"
};

// Declarar as filas DLQ
foreach (var queueName in dlqQueues)
{
    try
    {
        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        Console.WriteLine($"✅ Fila {queueName} verificada/criada");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Erro ao configurar fila {queueName}: {ex.Message}");
    }
}

// Configurar consumer para cada fila DLQ
foreach (var queueName in dlqQueues)
{
    try
    {
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var headers = ea.BasicProperties.Headers;

                Console.WriteLine($"\n💀 [{DateTime.Now:HH:mm:ss}] Dead Letter detectada em {queueName}:");
                Console.WriteLine($"   📄 Mensagem: {message}");

                // Extrair informações dos headers
                if (headers != null)
                {
                    if (headers.TryGetValue("x-exception", out var exception) && exception != null)
                    {
                        Console.WriteLine($"   ❌ Exceção: {GetHeaderValue(exception)}");
                    }

                    if (headers.TryGetValue("x-retry-count", out var retryCount) && retryCount != null)
                    {
                        Console.WriteLine($"   🔄 Tentativas: {GetHeaderValue(retryCount)}");
                    }

                    if (headers.TryGetValue("x-failed-at", out var failedAt) && failedAt != null)
                    {
                        var timestamp = Convert.ToInt64(GetHeaderValue(failedAt));
                        var dateTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
                        Console.WriteLine($"   ⏰ Falhou em: {dateTime:yyyy-MM-dd HH:mm:ss}");
                    }

                    if (headers.TryGetValue("x-original-exchange", out var exchange) && exchange != null)
                    {
                        Console.WriteLine($"   🔀 Exchange original: {GetHeaderValue(exchange)}");
                    }

                    if (headers.TryGetValue("x-original-routing-key", out var routingKey) && routingKey != null)
                    {
                        Console.WriteLine($"   🔑 Routing key original: {GetHeaderValue(routingKey)}");
                    }
                }

                Console.WriteLine($"   📋 Fila DLQ: {queueName}");
                Console.WriteLine();

                await channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao processar mensagem DLQ: {ex.Message}");
                // Nack com requeue=true para tentar novamente
                await channel.BasicNackAsync(ea.DeliveryTag, false, true);
            }
        };

        await channel.BasicConsumeAsync(queueName, false, consumer);
        Console.WriteLine($"🔔 Monitorando fila DLQ: {queueName}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Erro ao configurar consumer para {queueName}: {ex.Message}");
    }
}

// Função auxiliar para extrair valores dos headers
static string GetHeaderValue(object headerValue)
{
    if (headerValue == null)
    {
        return "N/A";
    }

    if (headerValue is byte[] bytes)
    {
        return Encoding.UTF8.GetString(bytes);
    }

    return headerValue.ToString() ?? "N/A";
}

Console.WriteLine("\n✅ DLQ Monitor iniciado. Aguardando mensagens...");
Console.WriteLine("Pressione Ctrl+C para sair");

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