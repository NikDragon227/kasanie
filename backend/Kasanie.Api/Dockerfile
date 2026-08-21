FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Kasanie.Api/Kasanie.Api.csproj Kasanie.Api/
RUN dotnet restore Kasanie.Api/Kasanie.Api.csproj
COPY Kasanie.Api/ Kasanie.Api/
RUN dotnet publish Kasanie.Api/Kasanie.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/* && mkdir -p /app/keys && chown $APP_UID:$APP_UID /app/keys
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Kasanie.Api.dll"]
