# 1. Use the .NET SDK to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# 2. Copy the .csproj and restore the packages you see in your screenshot
COPY SuperAppBackend.csproj ./
RUN dotnet restore

# 3. Copy the rest of your code (Controllers, Program.cs, etc.) and build it
COPY . ./
RUN dotnet publish -c Release -o out

# 4. Use the .NET Runtime to run the app
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# 5. Tell Render to use port 8080
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SuperAppBackend.dll"]