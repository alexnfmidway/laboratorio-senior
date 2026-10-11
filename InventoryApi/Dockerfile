FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["InventoryApi/InventoryApi.csproj", "InventoryApi/"]
RUN dotnet restore "InventoryApi/InventoryApi.csproj"
COPY . .
WORKDIR "/src/InventoryApi"
RUN dotnet publish "InventoryApi.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "InventoryApi.dll"]
