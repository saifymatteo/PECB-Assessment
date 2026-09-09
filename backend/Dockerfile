# Build the API (SDK), then ship only the runtime output - final image ~100 MB.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first with just the project files so Docker layer-caches the packages.
COPY src/SupportDesk.Api/SupportDesk.Api.csproj src/SupportDesk.Api/
RUN dotnet restore src/SupportDesk.Api/SupportDesk.Api.csproj

COPY src/ ./src/
RUN dotnet publish src/SupportDesk.Api/SupportDesk.Api.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# The container listens on 8080; compose maps it to host port 5000.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SupportDesk.Api.dll"]
