using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

Console.WriteLine("♻️ DLQ Reprocessor");

var rabbitMQHost = Environment.GetEnvironmentVariable("RabbitMQ__Host") ?? "localhost";
Console.WriteLine($"Conectando ao RabbitMQ em: {rabbitMQHost}");

var factory = new ConnectionFactory() { HostName = rabbitMQHost };
using var connection = await factory.CreateConnectionAsync();
using var channel = await connection.CreateChannelAsync();

// Verificar/criar filas DLQ  
var dlqQueues = new List<string> { "order-processor.dead", "email-service.dead", "inventory-service.dead" };

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

        // Verificar se há mensagens  
        var queueInfo = await channel.QueueDeclarePassiveAsync(queueName);
        Console.WriteLine($"   📊 {queueInfo.MessageCount} mensagens na fila");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Erro ao configurar fila {queueName}: {ex.Message}");
    }
}

// Em ambiente Docker, usar modo automático  
var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

if (isDocker)
{
    Console.WriteLine("\n🔄 Executando em modo automático (Docker)");

    // Verificar se há mensagens em alguma fila  
    bool hasMessages = false;
    string queueWithMessages = "";

    foreach (var queueName in dlqQueues)
    {
        try
        {
            var queueInfo = await channel.QueueDeclarePassiveAsync(queueName);
            if (queueInfo.MessageCount > 0)
            {
                hasMessages = true;
                queueWithMessages = queueName;
                Console.WriteLine($"🔍 Encontradas {queueInfo.MessageCount} mensagens em {queueName}");
            }
        }
        catch
        {
            // Ignorar erros  
        }
    }

    if (hasMessages)
    {
        Console.WriteLine($"♻️ Reprocessando mensagens de {queueWithMessages}...");
        await ReprocessAllAsync(channel, queueWithMessages);
    }
    else
    {
        Console.WriteLine("ℹ️ Nenhuma mensagem encontrada para reprocessar.");
    }
}
else
{
    // Modo interativo para uso fora do Docker  
    Console.WriteLine("\nOpções:");
    Console.WriteLine("1 - Reprocessar todas as mensagens");
    Console.WriteLine("2 - Reprocessar mensagem específica");
    Console.WriteLine("3 - Sair");
    Console.Write("Escolha uma opção: ");

    var option = Console.ReadLine();

    switch (option)
    {
        case "1":
            Console.WriteLine("\nEscolha a fila DLQ:");
            for (int i = 0; i < dlqQueues.Count; i++)
            {
                Console.WriteLine($"{i + 1} - {dlqQueues[i]}");
            }
            Console.Write("Fila: ");

            if (int.TryParse(Console.ReadLine(), out int queueIndex) && queueIndex > 0 && queueIndex <= dlqQueues.Count)
            {
                var selectedQueue = dlqQueues[queueIndex - 1];
                await ReprocessAllAsync(channel, selectedQueue);
            }
            else
            {
                Console.WriteLine("❌ Opção inválida!");
            }
            break;

        case "2":
            Console.WriteLine("\nEscolha a fila DLQ:");
            for (int i = 0; i < dlqQueues.Count; i++)
            {
                Console.WriteLine($"{i + 1} - {dlqQueues[i]}");
            }
            Console.Write("Fila: ");

            if (int.TryParse(Console.ReadLine(), out int queueIdx) && queueIdx > 0 && queueIdx <= dlqQueues.Count)
            {
                var selectedQueue = dlqQueues[queueIdx - 1];

                Console.Write("Digite o ID da mensagem: ");
                var messageId = Console.ReadLine();

                if (string.IsNullOrEmpty(messageId))
                {
                    Console.WriteLine("❌ ID da mensagem não pode ser vazio!");
                }
                else
                {
                    await ReprocessSpecificAsync(channel, selectedQueue, messageId);
                }
            }
            else
            {
                Console.WriteLine("❌ Opção inválida!");
            }
            break;

        case "3":
            Console.WriteLine("👋 Saindo...");
            break;

        default:
            Console.WriteLine("❌ Opção inválida!");
            break;
    }
}

static async Task ReprocessSpecificAsync(IChannel channel, string queueName, string messageId)
{
    Console.WriteLine($"🔍 Procurando mensagem com ID {messageId} na fila {queueName}...");

    var found = false;
    var cts = new CancellationTokenSource();
    var consumer = new AsyncEventingBasicConsumer(channel);

    consumer.ReceivedAsync += async (model, ea) =>
    {
        try
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var headers = ea.BasicProperties.Headers;

            // Verificar se contém o ID (simplificado - em produção use JSON parsing adequado)
            if (message.Contains(messageId))
            {
                Console.WriteLine($"✅ Mensagem encontrada! Reprocessando...");

                // Determinar a routing key original
                string routingKey = "order.created"; // Default
                if (headers != null && headers.TryGetValue("x-original-routing-key", out var originalRoutingKey) && originalRoutingKey != null)
                {
                    if (originalRoutingKey is byte[] bytes)
                    {
                        routingKey = Encoding.UTF8.GetString(bytes);
                    }
                    else
                    {
                        routingKey = originalRoutingKey.ToString() ?? "order.created";
                    }
                }

                // Determinar a exchange original
                string exchange = "orders"; // Default
                if (headers != null && headers.TryGetValue("x-original-exchange", out var originalExchange) && originalExchange != null)
                {
                    if (originalExchange is byte[] bytes)
                    {
                        exchange = Encoding.UTF8.GetString(bytes);
                    }
                    else
                    {
                        exchange = originalExchange.ToString() ?? "orders";
                    }
                }

                // Criar novas propriedades (sem histórico de retry)
                var properties = new BasicProperties();

                // Republicar na exchange original com routing key original
                Console.WriteLine($"📤 Republicando na exchange '{exchange}' com routing key '{routingKey}'");
                await channel.BasicPublishAsync(exchange, routingKey, false, properties, ea.Body);
                await channel.BasicAckAsync(ea.DeliveryTag, false);

                found = true;
                Console.WriteLine("✅ Mensagem reprocessada com sucesso!");
                cts.Cancel(); // Cancelar o consumer após encontrar
            }
            else
            {
                // Devolver para a DLQ (nack + requeue)
                await channel.BasicNackAsync(ea.DeliveryTag, false, true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao processar mensagem: {ex.Message}");
            await channel.BasicNackAsync(ea.DeliveryTag, false, true);
        }
    };

    // Consumir apenas uma mensagem por vez
    await channel.BasicQosAsync(0, 1, false);
    var consumerTag = await channel.BasicConsumeAsync(queueName, false, consumer);

    try
    {
        // Aguardar até encontrar a mensagem ou timeout (30 segundos)
        var timeoutTask = Task.Delay(30000, cts.Token);
        try
        {
            await timeoutTask;
            if (!found)
            {
                Console.WriteLine("❌ Timeout: Mensagem não encontrada após 30 segundos.");
            }
        }
        catch (OperationCanceledException)
        {
            // Normal quando encontramos a mensagem e cancelamos o token
        }
    }
    finally
    {
        // Cancelar o consumer
        try
        {
            await channel.BasicCancelAsync(consumerTag);
        }
        catch
        {
            // Ignorar erros ao cancelar
        }
    }

    if (!found)
    {
        Console.WriteLine("❌ Mensagem não encontrada na DLQ.");
    }
}

static async Task ReprocessAllAsync(IChannel channel, string queueName)
{
    Console.WriteLine($"🔄 Reprocessando todas as mensagens da fila {queueName}...");

    var messageCount = 0;
    var cts = new CancellationTokenSource();
    var consumer = new AsyncEventingBasicConsumer(channel);
    var processingComplete = new TaskCompletionSource<bool>();

    // Verificar quantas mensagens existem na fila
    var queueInfo = await channel.QueueDeclarePassiveAsync(queueName);
    var totalMessages = queueInfo.MessageCount;

    if (totalMessages == 0)
    {
        Console.WriteLine("ℹ️ Não há mensagens na fila para reprocessar.");
        return;
    }

    Console.WriteLine($"ℹ️ Encontradas {totalMessages} mensagens para reprocessar.");

    consumer.ReceivedAsync += async (model, ea) =>
    {
        try
        {
            var body = ea.Body.ToArray();
            var headers = ea.BasicProperties.Headers;
            messageCount++;

            // Determinar a routing key original
            string routingKey = "order.created"; // Default
            if (headers != null && headers.TryGetValue("x-original-routing-key", out var originalRoutingKey) && originalRoutingKey != null)
            {
                if (originalRoutingKey is byte[] bytes)
                {
                    routingKey = Encoding.UTF8.GetString(bytes);
                }
                else
                {
                    routingKey = originalRoutingKey.ToString() ?? "order.created";
                }
            }

            // Determinar a exchange original
            string exchange = "orders"; // Default
            if (headers != null && headers.TryGetValue("x-original-exchange", out var originalExchange) && originalExchange != null)
            {
                if (originalExchange is byte[] bytes)
                {
                    exchange = Encoding.UTF8.GetString(bytes);
                }
                else
                {
                    exchange = originalExchange.ToString() ?? "orders";
                }
            }

            Console.WriteLine($"♻️ [{messageCount}/{totalMessages}] Reprocessando mensagem para {exchange}/{routingKey}");

            var properties = new BasicProperties();

            await channel.BasicPublishAsync(exchange, routingKey, false, properties, ea.Body);
            await channel.BasicAckAsync(ea.DeliveryTag, false);

            // Se processamos todas as mensagens, completar a tarefa
            if (messageCount >= totalMessages)
            {
                processingComplete.TrySetResult(true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao reprocessar mensagem: {ex.Message}");
            await channel.BasicNackAsync(ea.DeliveryTag, false, true);
        }
    };

    // Consumir mensagens
    var consumerTag = await channel.BasicConsumeAsync(queueName, false, consumer);

    try
    {
        // Aguardar até processar todas as mensagens ou timeout (60 segundos)
        var timeoutTask = Task.Delay(60000, cts.Token);
        var completedTask = await Task.WhenAny(processingComplete.Task, timeoutTask);

        if (completedTask == timeoutTask)
        {
            Console.WriteLine("⚠️ Timeout: Nem todas as mensagens foram processadas no tempo limite.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Erro durante o reprocessamento: {ex.Message}");
    }
    finally
    {
        try
        {
            await channel.BasicCancelAsync(consumerTag);
        }
        catch
        {
        }
    }

    Console.WriteLine($"✅ {messageCount} mensagens reprocessadas!");
}