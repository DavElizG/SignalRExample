# Use the .NET SDK for building the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["SignalR/SignalR.csproj", "SignalR/"]
COPY ["DataAcces/DataAcces.csproj", "DataAcces/"]
COPY ["Services/Services.csproj", "Services/"]
RUN dotnet restore "SignalR/SignalR.csproj"

# Copy all files and build
COPY . .
WORKDIR "/src/SignalR"
RUN dotnet build "SignalR.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "SignalR.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Build the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 80
EXPOSE 443

ENTRYPOINT ["dotnet", "SignalR.dll"]