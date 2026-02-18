# BUILD
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar ambos proyectos
COPY Middleware_Core/ Middleware_Core/
COPY Middleware.API/ Middleware.API/

# Restaurar dependencias
RUN dotnet restore Middleware.API/Middleware.API.csproj

# Publicar la aplicación y todas sus dependencias
RUN dotnet publish Middleware.API/Middleware.API.csproj -c Release -o /src/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /src/publish/ .
ENTRYPOINT ["dotnet", "Middleware.API.dll"]
