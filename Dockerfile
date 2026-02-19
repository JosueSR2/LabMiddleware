FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar archivos de solución/proyectos primero para aprovechar cache de restore
COPY LabMiddleware.sln ./
COPY Middleware.API/Middleware.API.csproj Middleware.API/
COPY Middleware_Core/Middleware_Core.csproj Middleware_Core/
RUN dotnet restore Middleware.API/Middleware.API.csproj

# Copiar el resto del código y publicar
COPY Middleware.API/ Middleware.API/
COPY Middleware_Core/ Middleware_Core/
RUN dotnet publish Middleware.API/Middleware.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Carpeta esperada por Program.cs para monitorear archivos
RUN mkdir -p /home/linkdicom/Proyectos/LabMiddleware/TestingResources

COPY --from=build /app/publish/ ./

EXPOSE 8080
EXPOSE 5001

ENTRYPOINT ["dotnet", "Middleware.Core.dll"]
