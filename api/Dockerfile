# Imagem da API. O build roda no GitHub Actions (nunca na EC2); o servidor só faz pull e up.
# Contexto de build: a pasta api/.

# O estágio de build roda sempre na arquitetura do runner e compila PARA a arquitetura alvo
# (-a $TARGETARCH), então gerar a imagem arm64 não passa por emulação (QEMU).
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

# Só os .csproj primeiro: o restore fica em cache enquanto as dependências não mudarem.
COPY src/Dueto.Core/Dueto.Core.csproj src/Dueto.Core/
COPY src/Dueto.Infrastructure/Dueto.Infrastructure.csproj src/Dueto.Infrastructure/
COPY src/Dueto.Api/Dueto.Api.csproj src/Dueto.Api/
RUN dotnet restore src/Dueto.Api/Dueto.Api.csproj -a $TARGETARCH

COPY src/ src/
RUN dotnet publish src/Dueto.Api/Dueto.Api.csproj \
    -c Release -a $TARGETARCH --no-restore --no-self-contained -o /app /p:UseAppHost=false

# Imagem final: só o runtime (Alpine, menor), sem SDK, rodando sem root.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
COPY --from=build /app ./

# Globalização invariante: dispensa o pacote ICU (~30 MB) e o código só usa InvariantCulture.
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=0

# Usuário não privilegiado que já vem na imagem base.
USER $APP_UID
EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=3 \
    CMD wget -q -O /dev/null http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "Dueto.Api.dll"]
