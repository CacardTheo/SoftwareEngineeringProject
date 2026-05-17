FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY EasySaveLogServer.csproj .
RUN dotnet restore EasySaveLogServer.csproj
COPY . .
RUN dotnet publish EasySaveLogServer.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV EASYSAVE_LOG_PORT=5132
EXPOSE 5132
ENTRYPOINT ["dotnet", "EasySaveLogServer.dll"]
