FROM mcr.microsoft.com/dotnet/sdk:11.0.100 AS build
WORKDIR /src
COPY NuGet.Config global.json ./
COPY src/FlowOps.Api/FlowOps.Api.csproj src/FlowOps.Api/
RUN dotnet restore src/FlowOps.Api
COPY src/ src/
RUN dotnet publish src/FlowOps.Api --no-restore -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:11.0.0
WORKDIR /app
COPY --from=build /out ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "FlowOps.Api.dll"]
