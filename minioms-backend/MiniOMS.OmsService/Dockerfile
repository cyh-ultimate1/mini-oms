# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish MiniOMS.OmsService/MiniOMS.OmsService.csproj -c Release -o /app

# ---- final stage ----
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "MiniOMS.OmsService.dll"]