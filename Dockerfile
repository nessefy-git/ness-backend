FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy everything first
COPY . .

# Restore using the correct .csproj
RUN dotnet restore SocialMediaAuthAPI.csproj

# Publish using the same project path
RUN dotnet publish SocialMediaAuthAPI.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Development
ENV ASPNETCORE_URLS=http://0.0.0.0:5223

EXPOSE 5223
ENTRYPOINT ["dotnet", "SocialMediaAuthAPI.dll"]