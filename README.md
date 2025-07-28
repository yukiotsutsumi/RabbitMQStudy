# RabbitMQ Study

Um projeto de estudo completo sobre mensageria com RabbitMQ, implementando padrões de comunicação assíncrona entre microsserviços.

## 📋 Visão Geral

Este projeto demonstra a implementação de um sistema de mensageria usando RabbitMQ com .NET 9, incluindo:

- **API REST** para receber pedidos
- **Consumers** para processamento assíncrono (Email e Inventário)
- **Ferramentas de monitoramento** (Health Checker, DLQ Monitor, DLQ Reprocessor)
- **Políticas de retry** e tratamento de falhas
- **Dead Letter Queues (DLQ)** para mensagens com falha

## 🏗️ Arquitetura

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Client/API    │───▶│   RabbitMQ      │───▶│   Consumers     │
│   (Producer)    │    │   (Message      │    │   (Email &      │
│                 │    │    Broker)      │    │   Inventory)    │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                              │
                              ▼
                       ┌─────────────────┐
                       │  Monitoring     │
                       │  Tools          │
                       │  (Health, DLQ)  │
                       └─────────────────┘
```

## 📁 Estrutura do Projeto

```
RabbitMQStudy/
├── shared/                          # Projetos compartilhados
│   ├── RabbitMQStudy.Application/   # Comandos, handlers e interfaces
│   ├── RabbitMQStudy.Domain/        # Entidades e eventos de domínio
│   └── RabbitMQStudy.Infrastructure/ # Implementações de infraestrutura
├── src/                             # Microsserviços principais
│   ├── RabbitMQStudy.API/           # API REST (Producer)
│   ├── RabbitMQStudy.EmailService/  # Consumer de emails
│   └── RabbitMQStudy.InventoryService/ # Consumer de inventário
├── tools/                           # Ferramentas de monitoramento
│   ├── RabbitMQStudy.HealthChecker/ # Monitor de saúde das filas
│   ├── RabbitMQStudy.DLQMonitor/    # Monitor de Dead Letter Queues
│   └── RabbitMQStudy.DLQReprocessor/ # Reprocessador de mensagens DLQ
├── tests/                           # Testes unitários
├── docker-compose.yml               # Configuração dos containers
└── README.md                        # Esta documentação
```

## 🚀 Como Executar

### Pré-requisitos

- Docker e Docker Compose
- .NET 9 SDK (para desenvolvimento local)

### Executando com Docker

1. **Clone o repositório:**
   ```bash
   git clone https://github.com/yukiotsutsumi/RabbitMQStudy
   cd RabbitMQStudy
   ```

2. **Inicie todos os serviços:**
   ```bash
   docker compose up -d
   ```

3. **Verifique se os serviços estão rodando:**
   ```bash
   docker compose ps
   ```

### Serviços Disponíveis

| Serviço | URL | Descrição |
|---------|-----|-----------|
| API | http://localhost:5278 | API REST para criar pedidos |
| RabbitMQ Management | http://localhost:15672 | Interface web do RabbitMQ |
| Swagger | http://localhost:5278/swagger | Documentação da API |

**Credenciais RabbitMQ:** `guest/guest`

## 📡 Endpoints da API

### Criar Pedido
```http
POST /api/orders
Content-Type: application/json

{
  "customerName": "John Doe",
  "customerEmail": "John@email.com",
  "items": [
    {
      "productId": "550e8400-e29b-41d4-a716-446655440000",
      "productName": "Produto A",
      "quantity": 2,
      "unitPrice": 29.99
    }
  ]
}
```

### Health Check
```http
GET /health
```

## 🔄 Fluxo de Mensagens

1. **Cliente** envia requisição POST para `/api/orders`
2. **API** cria o pedido e publica evento `order.created` no RabbitMQ
3. **Email Service** consome a mensagem e processa envio de email
4. **Inventory Service** consome a mensagem e atualiza o estoque
5. **Health Checker** monitora o status das filas periodicamente

## 🛠️ Monitoramento

### Health Checker
Monitora periodicamente o status das filas principais e DLQs:
```bash
docker logs rabbitmq-health-checker
```

### RabbitMQ Management UI
Acesse http://localhost:15672 para:
- Visualizar filas e exchanges
- Monitorar mensagens em tempo real
- Verificar conexões ativas
- Analisar estatísticas de throughput

### Logs dos Serviços
```bash
# API
docker logs rabbitmq-api

# Email Service
docker logs rabbitmq-email-service

# Inventory Service
docker logs rabbitmq-inventory-service

# DLQ Monitor
docker logs rabbitmq-dlq-monitor

# DLQ Reprocessor
docker logs rabbitmq-dlq-reprocessor
```

## 🧪 Testando Falhas

Para testar o comportamento de falhas e retry policies:

1. **Simular falhas nos consumers** (implementar falhas aleatórias no código)
2. **Parar um consumer temporariamente:**
   ```bash
   docker compose stop email-service
   ```
3. **Enviar mensagens** via API
4. **Observar mensagens acumulando** na fila via RabbitMQ Management
5. **Reiniciar o consumer:**
   ```bash
   docker compose start email-service
   ```

## ⚙️ Configuração

### Variáveis de Ambiente

| Variável | Descrição | Valor Padrão |
|----------|-----------|--------------|
| `RabbitMQ__Host` | Host do RabbitMQ | `rabbitmq` |
| `RabbitMQ__Port` | Porta do RabbitMQ | `5672` |
| `RabbitMQ__UserName` | Usuário do RabbitMQ | `guest` |
| `RabbitMQ__Password` | Senha do RabbitMQ | `guest` |
| `RabbitMQ__VirtualHost` | Virtual Host | `/` |

### Configuração de Retry Policy

O projeto implementa políticas de retry usando uma implementação personalizada:
- **Tentativas:** 3 tentativas
- **Backoff:** Exponencial (2^tentativa segundos) com jitter
- **Mecanismo:** Filas de delay temporárias com TTL
- **DLQ:** Mensagens que falham após todas as tentativas vão para Dead Letter Queue

## 🐛 Troubleshooting

### Problemas Comuns

1. **"Socket hung up" ou "Connection refused"**
   - Verifique se o RabbitMQ está rodando: `docker compose ps`
   - Aguarde o health check do RabbitMQ passar

2. **Containers não iniciam**
   - Reconstrua as imagens: `docker compose build`
   - Verifique os logs: `docker compose logs`

3. **Mensagens não são consumidas**
   - Verifique se os consumers estão rodando
   - Verifique os bindings no RabbitMQ Management UI

### Comandos Úteis

```bash
# Reconstruir e reiniciar tudo
docker compose down
docker compose build
docker compose up -d

# Ver logs em tempo real
docker compose logs -f

# Limpar tudo (cuidado: remove volumes)
docker compose down -v
docker system prune -a
```

## 🎯 Próximos Passos

- [ ] Implementação de banco de dados
- [ ] Implementar autenticação e autorização
- [ ] Adicionar métricas com Prometheus/Grafana
- [ ] Implementar circuit breaker
- [ ] Adicionar testes de integração
- [ ] Implementar saga pattern para transações distribuídas
- [ ] Adicionar tracing distribuído com OpenTelemetry

## 📚 Recursos Adicionais

- [RabbitMQ Documentation](https://www.rabbitmq.com/documentation.html)
- [RabbitMQ .NET Client Guide](https://www.rabbitmq.com/dotnet.html)
- [MediatR Documentation](https://github.com/jbogard/MediatR)
- [Polly Documentation](https://github.com/App-vNext/Polly)

## 🤝 Contribuindo

1. Fork o projeto
2. Crie uma branch para sua feature (`git checkout -b feature/AmazingFeature`)
3. Commit suas mudanças (`git commit -m 'Add some AmazingFeature'`)
4. Push para a branch (`git push origin feature/AmazingFeature`)
5. Abra um Pull Request

## 📄 Propriedade  
  
Este projeto foi desenvolvido para fins de estudo pessoal sobre mensageria e arquitetura de microsserviços.

--