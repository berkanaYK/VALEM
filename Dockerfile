FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/VALE.Contracts/VALE.Contracts.csproj src/VALE.Contracts/
COPY src/VALE.Api/VALE.Api.csproj src/VALE.Api/
RUN dotnet restore src/VALE.Api/VALE.Api.csproj

COPY src/VALE.Contracts/ src/VALE.Contracts/
COPY src/VALE.Api/ src/VALE.Api/
RUN dotnet publish src/VALE.Api/VALE.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "VALE.Api.dll"]

