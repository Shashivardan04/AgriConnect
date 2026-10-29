# Restore before copying the rest of the source, so a code change does not
# re-download every package.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY AgriConnect.sln .
COPY AgriConnect/AgriConnect.csproj AgriConnect/
RUN dotnet restore AgriConnect/AgriConnect.csproj

COPY . .
RUN dotnet publish AgriConnect/AgriConnect.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Uploaded photos are bind-mounted here by compose; create it so a first write
# works even when the volume is absent.
RUN mkdir -p wwwroot/uploads

EXPOSE 8080
ENTRYPOINT ["dotnet", "AgriConnect.dll"]
