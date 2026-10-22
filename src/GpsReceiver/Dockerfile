FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY src/GpsReceiver/ src/GpsReceiver/
RUN dotnet publish src/GpsReceiver/GpsReceiver.csproj -c Release -o /app --no-self-contained
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV GPS_PORT=5027 GPS_CAPTURE_DIR=/data/captures
USER $APP_UID
EXPOSE 5027/tcp
EXPOSE 9091/tcp
ENTRYPOINT ["dotnet", "GpsReceiver.dll"]
