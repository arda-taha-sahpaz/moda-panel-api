FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["ModaPanelApi.csproj", "./"]
RUN dotnet restore "ModaPanelApi.csproj"

COPY . .
RUN dotnet publish "ModaPanelApi.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "ModaPanelApi.dll"]