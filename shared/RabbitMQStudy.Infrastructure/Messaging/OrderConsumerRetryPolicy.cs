using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace RabbitMQStudy.Infrastructure.Messaging
{
    public class OrderConsumerRetryPolicy(IChannel channel, string queueName, int maxRetries = 3)
    {
        private static readonly Dictionary<string, int> _retryCounters = [];

        public async Task HandleFailureAsync(BasicDeliverEventArgs ea, string messageId, Exception exception)
        {
            // Usar o messageId para rastrear tentativas
            var key = $"{queueName}:{messageId}";

            if (!_retryCounters.ContainsKey(key))
            {
                _retryCounters[key] = 0;
            }

            _retryCounters[key]++;
            var currentRetryCount = _retryCounters[key];

            Console.WriteLine($"Debug: messageId={messageId}, tentativa={currentRetryCount}, max={maxRetries}");

            if (currentRetryCount >= maxRetries)
            {
                Console.WriteLine($"❌ Número máximo de retentativas atingido ({maxRetries}). Enviando para DLQ: {queueName}.dead");

                // Remover do contador (limpeza)
                _retryCounters.Remove(key);

                // Rejeitar sem requeue - vai para DLQ
                await channel.BasicNackAsync(ea.DeliveryTag, false, false);

                Console.WriteLine($"✅ Mensagem enviada para DLQ: {queueName}.dead");
                return;
            }

            Console.WriteLine($"🔄 Falha na tentativa {currentRetryCount}. Agendando retry #{currentRetryCount + 1}...");

            // Calcular delay com backoff exponencial
            var delayMs = CalculateBackoffDelay(currentRetryCount);
            Console.WriteLine($"⏱️ Agendando retry em {delayMs}ms");

            // Aguardar o delay
            await Task.Delay(delayMs);

            // Rejeitar e recolocar na fila (requeue=true)
            await channel.BasicNackAsync(ea.DeliveryTag, false, true);

            Console.WriteLine($"✅ Retry agendado para mensagem");
        }

        private static int CalculateBackoffDelay(int retryCount)
        {
            var baseDelay = 1000;
            var maxDelay = 30000;

            var delay = Math.Min(maxDelay, baseDelay * Math.Pow(2, retryCount - 1));
            var jitter = Random.Shared.Next(0, 500);

            return (int)delay + jitter;
        }
    }
}