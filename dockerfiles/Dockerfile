# Build em duas etapas: o SDK compila e o runtime (imagem menor) executa
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY SerenoAPI.csproj ./
RUN dotnet restore "./SerenoAPI.csproj"

COPY . .
RUN dotnet publish "./SerenoAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Usuário sem privilégio de administrador (boa prática de segurança)
RUN addgroup --system appgroup && \
    adduser --system --ingroup appgroup --no-create-home appuser

COPY --from=build /app/publish .

RUN mkdir -p /app/logs && chown -R appuser:appgroup /app

USER appuser

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "SerenoAPI.dll"]
