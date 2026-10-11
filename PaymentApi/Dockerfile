FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["PaymentApi/PaymentApi.csproj", "PaymentApi/"]
RUN dotnet restore "PaymentApi/PaymentApi.csproj"
COPY . .
WORKDIR "/src/PaymentApi"
RUN dotnet publish "PaymentApi.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PaymentApi.dll"]
