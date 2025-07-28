FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar o projeto
COPY ["tools/RabbitMQStudy.DLQMonitor/RabbitMQStudy.DLQMonitor.csproj", "tools/RabbitMQStudy.DLQMonitor/"]

# Restaurar dependências
RUN dotnet restore "tools/RabbitMQStudy.DLQMonitor/RabbitMQStudy.DLQMonitor.csproj"

# Copiar o resto do código
COPY . .

# Construir o projeto
WORKDIR "/src/tools/RabbitMQStudy.DLQMonitor"
RUN dotnet build "RabbitMQStudy.DLQMonitor.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "RabbitMQStudy.DLQMonitor.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RabbitMQStudy.DLQMonitor.dll"]