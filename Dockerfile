# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore with only the project files so the restore layer is cached.
COPY src/TaskFlow.Core/TaskFlow.Core.csproj src/TaskFlow.Core/
COPY src/TaskFlow.Data/TaskFlow.Data.csproj src/TaskFlow.Data/
COPY src/TaskFlow.Web/TaskFlow.Web.csproj src/TaskFlow.Web/
RUN dotnet restore src/TaskFlow.Web/TaskFlow.Web.csproj

COPY src/ src/
RUN dotnet publish src/TaskFlow.Web/TaskFlow.Web.csproj \
    -c Release \
    -o /app \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app .

# Writable directories for the SQLite database and the rolling log file.
RUN mkdir -p /data /app/Logs \
    && chown -R ${APP_UID:-1654} /data /app/Logs

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "TaskFlow.Web.dll"]
