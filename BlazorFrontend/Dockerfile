FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar e restaurar
COPY BlazorFrontend/BlazorFrontend.csproj BlazorFrontend/
RUN dotnet restore BlazorFrontend/BlazorFrontend.csproj

# Copiar tudo e publicar
COPY BlazorFrontend/ BlazorFrontend/
WORKDIR /src/BlazorFrontend
RUN dotnet publish BlazorFrontend.csproj -c Release -o /app/publish

# Servir como site estático com nginx
FROM nginx:alpine AS final
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY BlazorFrontend/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80

