using RabbitMQ.Client;

Console.WriteLine("🏥 Health Checker iniciado");

var rabbitMQHost = Environment.GetEnvironmentVariable("RabbitMQ__Host") ?? "localhost";
Console.WriteLine($"Conectando ao RabbitMQ em: {rabbitMQHost}");

var factory = new ConnectionFactory() { HostName = rabbitMQHost };

// Lista de filas para monitorar
var queues = new Dictionary<string, int>
{
    // Nome da fila e limite de mensagens
    { "order-processor", 100 },
    { "email-service", 100 },
    { "inventory-service", 100 }
};

// Lista de filas DLQ para monitorar
var dlqQueues = new Dictionary<string, int>
{
    // Nome da fila DLQ e limite de mensagens
    { "order-processor.dead", 10 },
    { "email-service.dead", 10 },
    { "inventory-service.dead", 10 }
};

// Verificar/criar filas
try
{
    using var setupConnection = await factory.CreateConnectionAsync();
    using var setupChannel = await setupConnection.CreateChannelAsync();

    Console.WriteLine("Verificando/criando filas...");

    // Declarar exchange
    await setupChannel.ExchangeDeclareAsync(
        exchange: "orders",
        type: "topic",
        durable: true,
        autoDelete: false);

    // Declarar exchange para DLQ
    await setupChannel.ExchangeDeclareAsync(
        exchange: "orders.dlx",
        type: "direct",
        durable: true,
        autoDelete: false);

    // Criar filas normais
    foreach (var queue in queues.Keys)
    {
        try
        {
            await setupChannel.QueueDeclareAsync(
                queue: queue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    { "x-dead-letter-exchange", "orders.dlx" },
                    { "x-dead-letter-routing-key", queue + ".dead" }
                });

            // Bind para a exchange
            await setupChannel.QueueBindAsync(
                queue: queue,
                exchange: "orders",
                routingKey: "order.created");

            Console.WriteLine($"✅ Fila {queue} verificada/criada");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao configurar fila {queue}: {ex.Message}");
        }
    }

    // Criar filas DLQ
    foreach (var queue in dlqQueues.Keys)
    {
        try
        {
            await setupChannel.QueueDeclareAsync(
                queue: queue,
                durable: true,
                exclusive: false,
                autoDelete: false);

            // Bind para a exchange DLX
            await setupChannel.QueueBindAsync(
                queue: queue,
                exchange: "orders.dlx",
                routingKey: queue);

            Console.WriteLine($"✅ Fila DLQ {queue} verificada/criada");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao configurar fila DLQ {queue}: {ex.Message}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Erro ao configurar filas: {ex.Message}");
}

// Loop principal de verificação
while (true)
{
    try
    {
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] Verificando status das filas:");

        // Verificar filas normais
        foreach (var queue in queues)
        {
            try
            {
                var queueInfo = await channel.QueueDeclarePassiveAsync(queue.Key);
                var messageCount = queueInfo.MessageCount;

                if (messageCount > queue.Value)
                {
                    Console.WriteLine($"⚠️ ALERTA: Fila {queue.Key} tem {messageCount} mensagens (limite: {queue.Value})");
                }
                else
                {
                    Console.WriteLine($"✅ Fila {queue.Key}: {messageCount} mensagens");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao verificar fila {queue.Key}: {ex.Message}");
            }
        }

        // Verificar filas DLQ
        foreach (var queue in dlqQueues)
        {
            try
            {
                var queueInfo = await channel.QueueDeclarePassiveAsync(queue.Key);
                var messageCount = queueInfo.MessageCount;

                if (messageCount > 0)
                {
                    Console.WriteLine($"⚠️ ALERTA: Fila DLQ {queue.Key} tem {messageCount} mensagens!");

                    if (messageCount > queue.Value)
                    {
                        Console.WriteLine($"🚨 CRÍTICO: Fila DLQ {queue.Key} excedeu o limite de {queue.Value} mensagens!");
                    }
                }
                else
                {
                    Console.WriteLine($"✅ Fila DLQ {queue.Key}: {messageCount} mensagens");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao verificar fila DLQ {queue.Key}: {ex.Message}");
            }
        }

        // Verificar conexão com RabbitMQ
        Console.WriteLine($"✅ Conexão com RabbitMQ: OK");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Erro de conectividade: {ex.Message}");
    }

    // Aguardar 30 segundos antes da próxima verificação
    await Task.Delay(30000);
}