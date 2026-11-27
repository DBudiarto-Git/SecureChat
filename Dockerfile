# Stage 1: Build (Nutzt das offizielle .NET 8 SDK zum Kompilieren)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Projektdateien kopieren und Abhängigkeiten wiederherstellen
COPY ["SecureChat.WebAPI/SecureChat.WebAPI.csproj", "SecureChat.WebAPI/"]
COPY ["SecureChat.Core/SecureChat.Core.csproj", "SecureChat.Core/"]
COPY ["SecureChat.Infrastructure/SecureChat.Infrastructure.csproj", "SecureChat.Infrastructure/"]
RUN dotnet restore "SecureChat.WebAPI/SecureChat.WebAPI.csproj"

# Gesamten Code kopieren und veröffentlichen
COPY . .
WORKDIR "/src/SecureChat.WebAPI"
RUN dotnet publish "SecureChat.WebAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime (Schlankes Linux-Image nur für den Betrieb)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SecureChat.WebAPI.dll"]
