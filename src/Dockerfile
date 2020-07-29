FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS installer-env

WORKDIR /src/app

COPY *.csproj .
RUN dotnet restore

COPY . .
RUN mkdir -p /home/site/wwwroot && \
    dotnet publish *.csproj --output /home/site/wwwroot --no-restore

FROM mcr.microsoft.com/azure-functions/dotnet:3.0-appservice
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

COPY --from=installer-env ["/home/site/wwwroot", "/home/site/wwwroot"]