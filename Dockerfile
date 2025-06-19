# --------- Stage 1: Build the application ---------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy .csproj and restore
COPY *.csproj ./
RUN dotnet restore

# Copy all files and build
COPY . .
RUN dotnet publish -c Release -o /app/publish

# --------- Stage 2: Run the application ---------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Open port (match the one your app listens to)
EXPOSE 5223

ENTRYPOINT ["dotnet", "SocialMediaAuthAPI.csproj"]