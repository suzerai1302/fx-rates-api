# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore (copy csproj/sln first for layer caching)
COPY FxRatesAPI.slnx ./
COPY src/FxRates.Core/FxRates.Core.csproj src/FxRates.Core/
COPY src/FxRates.Infrastructure/FxRates.Infrastructure.csproj src/FxRates.Infrastructure/
COPY src/FxRates.API/FxRates.API.csproj src/FxRates.API/
RUN dotnet restore src/FxRates.API/FxRates.API.csproj

# Build + publish
COPY . .
RUN dotnet publish src/FxRates.API/FxRates.API.csproj -c Release -o /app/publish

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FxRates.API.dll"]
