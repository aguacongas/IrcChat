# Adjust DOTNET_OS_VERSION as desired
ARG DOTNET_OS_VERSION="-alpine"
ARG DOTNET_SDK_VERSION=9.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_VERSION}${DOTNET_OS_VERSION} AS build
WORKDIR /src

# copy everything
COPY . ./
# restore as distinct layers
RUN dotnet restore
# build and publish a release
RUN dotnet publish /src/src/IrcChat.Client/IrcChat.Client.csproj -c Release -o /app

# final stage/image
FROM nginx:1.29.1-alpine3.22-slim AS final
WORKDIR /usr/share/nginx/html
COPY --from=publish /app/wwwroot .
COPY --from=build /src/src/IrcChat.Client/nginx.conf /etc/nginx/nginx.conf