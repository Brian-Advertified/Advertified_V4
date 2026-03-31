FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN echo '{"sdk":{"version":"10.0.201"}}' > /src/global.json
RUN dotnet restore src/Advertified.App/Advertified.App.csproj
RUN dotnet publish src/Advertified.App/Advertified.App.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .
COPY --from=build /src/database /app/database

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Advertified.App.dll"]
