FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/VALE.Contracts/VALE.Contracts.csproj src/VALE.Contracts/
COPY src/VALE.Api/VALE.Api.csproj src/VALE.Api/
RUN dotnet restore src/VALE.Api/VALE.Api.csproj

COPY src/VALE.Contracts/ src/VALE.Contracts/
COPY src/VALE.Api/ src/VALE.Api/
RUN dotnet publish src/VALE.Api/VALE.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /var/lib/valem/dpkeys \
    && chown -R $APP_UID:$APP_UID /var/lib/valem
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 CMD curl --fail --silent --show-error http://127.0.0.1:10000/health/ready || exit 1
USER $APP_UID
ENTRYPOINT ["dotnet", "VALE.Api.dll"]
