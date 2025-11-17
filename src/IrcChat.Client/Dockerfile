# Étape 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# copy everything
COPY . ./

# restore as distinct layers
RUN dotnet restore

# Build et publish l'application Blazor WASM
WORKDIR "/src/src/IrcChat.Client"
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Étape 2: Runtime avec Nginx
FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html

# Copier les fichiers Blazor WASM publiés
COPY --from=build /app/publish/wwwroot .

# Copier la configuration Nginx personnalisée
COPY --from=build /src/src/IrcChat.Client/nginx.conf /etc/nginx/nginx.conf

# Exposer le port 80
EXPOSE 80

# Démarrer Nginx
CMD ["nginx", "-g", "daemon off;"]
