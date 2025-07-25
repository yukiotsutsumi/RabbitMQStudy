using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

Console.WriteLine("♻️ DLQ Reprocessor");
Console.WriteLine("1 - Reprocessar todas as mensagens");
Console.WriteLine("2 - Reprocessar mensagem específica");
Console.Write("Escolha uma opção: ");

var option = Console.ReadLine();

var factory = new ConnectionFactory() { HostName = "localhost" };
using var connection = await factory.CreateConnectionAsync();
using var channel = await connection.CreateChannelAsync();

switch (option)
{
    case "1":
        await ReprocessAllAsync(channel);
        break;
    case "2":
        Console.Write("Digite o ID da mensagem: ");
        var messageId = Console.ReadLine();

        if (string.IsNullOrEmpty(messageId))
        {
            Console.WriteLine("❌ ID da mensagem não pode ser vazio!");
        }
        else
        {
            await ReprocessSpecificAsync(channel, messageId);
        }
        break;
    default:
        Console.WriteLine("Opção inválida");
        break;
}

static async Task ReprocessSpecificAsync(IChannel channel, string messageId)
{
    Console.WriteLine($"🔍 Procurando mensagem com ID {messageId} na DLQ...");

    var found = false;
    var consumer = new AsyncEventingBasicConsumer(channel);

    consumer.ReceivedAsync += async (model, ea) =>
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);

        // Verificar se contém o ID (simplificado - em produção use JSON parsing adequado)
        if (message.Contains(messageId))
        {
            Console.WriteLine($"✅ Mensagem encontrada! Reprocessando...");

            // Criar novas propriedades (sem histórico de retry)
            var properties = new BasicProperties();

            // Republicar na fila principal
            await channel.BasicPublishAsync("orders", "order.created", false, properties, ea.Body);
            await channel.BasicAckAsync(ea.DeliveryTag, false);

            found = true;
            Console.WriteLine("✅ Mensagem reprocessada com sucesso!");
        }
        else
        {
            // Devolver para a DLQ (nack + requeue)
            await channel.BasicNackAsync(ea.DeliveryTag, false, true);
        }
    };

    // Consumir apenas uma mensagem por vez
    await channel.BasicQosAsync(0, 1, false);
    await channel.BasicConsumeAsync("order-processor.dead", false, consumer);

    // Aguardar um tempo para processar
    await Task.Delay(5000);

    if (!found)
    {
        Console.WriteLine("❌ Mensagem não encontrada na DLQ.");
    }
}

static async Task ReprocessAllAsync(IChannel channel)
{
    Console.WriteLine("🔄 Reprocessando todas as mensagens da DLQ...");

    var messageCount = 0;
    var consumer = new AsyncEventingBasicConsumer(channel);

    consumer.ReceivedAsync += async (model, ea) =>
    {
        messageCount++;
        Console.WriteLine($"♻️ Reprocessando mensagem {messageCount}");

        var properties = new BasicProperties();

        await channel.BasicPublishAsync("orders", "order.created", false, properties, ea.Body);
        await channel.BasicAckAsync(ea.DeliveryTag, false);
    };

    await channel.BasicConsumeAsync("order-processor.dead", false, consumer);

    await Task.Delay(5000);
    Console.WriteLine($"✅ {messageCount} mensagens reprocessadas!");
}