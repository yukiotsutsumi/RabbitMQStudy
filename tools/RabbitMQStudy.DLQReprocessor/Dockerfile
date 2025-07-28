FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar o projeto
COPY ["tools/RabbitMQStudy.DLQReprocessor/RabbitMQStudy.DLQReprocessor.csproj", "tools/RabbitMQStudy.DLQReprocessor/"]

# Restaurar dependências
RUN dotnet restore "tools/RabbitMQStudy.DLQReprocessor/RabbitMQStudy.DLQReprocessor.csproj"

# Copiar o resto do código
COPY . .

# Construir o projeto
WORKDIR "/src/tools/RabbitMQStudy.DLQReprocessor"
RUN dotnet build "RabbitMQStudy.DLQReprocessor.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "RabbitMQStudy.DLQReprocessor.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RabbitMQStudy.DLQReprocessor.dll"]