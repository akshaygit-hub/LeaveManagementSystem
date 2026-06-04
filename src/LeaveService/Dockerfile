# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["src/Shared/Shared.csproj", "src/Shared/"]
COPY ["src/LeaveService/LeaveService.csproj", "src/LeaveService/"]
RUN dotnet restore "src/LeaveService/LeaveService.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/LeaveService"
RUN dotnet build "LeaveService.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "LeaveService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LeaveService.dll"]
