# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
ENV DataProtection__KeysPath=/var/lib/tattooflash/keys
RUN mkdir -p  /var/lib/tattooflash/keys \
	&& chown "$APP_UID" /var/lib/tattooflash/keys  \
	&& chmod 700 /var/lib/tattooflash/keys
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["TattooFlash.Web/TattooFlash.Web.csproj", "TattooFlash.Web/"]
RUN dotnet restore "./TattooFlash.Web/TattooFlash.Web.csproj"
COPY . .
WORKDIR "/src/TattooFlash.Web"
RUN dotnet build "./TattooFlash.Web.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./TattooFlash.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TattooFlash.Web.dll"]
