# Adjust DOTNET_OS_VERSION as desired
ARG DOTNET_OS_VERSION="-alpine"
ARG DOTNET_SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_VERSION}${DOTNET_OS_VERSION} AS build
WORKDIR /src

# Six Labors license — pass the file content as a build argument.
# On Render.com: set SIXLABORS_LICENSE_FILE in Environment > Build Environment Variables,
# with the full content of your sixlabors.lic file as the value.
# The file is written to a temp path only in the build stage and never reaches the final image.
ARG SIXLABORS_LICENSE_FILE

# copy everything
COPY . ./
# restore as distinct layers
RUN dotnet restore
# write the license file and publish — rm ensures it never leaks into the output
RUN printf '%s' "${SIXLABORS_LICENSE_FILE}" > /tmp/sixlabors.lic && \
    dotnet publish /src/src/IrcChat.Api/IrcChat.Api.csproj \
        -c Release \
        -o /app \
        -p:SixLaborsLicenseFile=/tmp/sixlabors.lic && \
    rm /tmp/sixlabors.lic

# final stage/image — license file is NOT present here
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_SDK_VERSION}
ENV ASPNETCORE_URLS http://+:8080
ENV ASPNETCORE_ENVIRONMENT Production
EXPOSE 8080
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT [ "dotnet", "IrcChat.Api.dll" ]
