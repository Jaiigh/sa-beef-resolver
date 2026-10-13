FROM node:22-bookworm-slim AS demo-client
WORKDIR /app
COPY package*.json ./
RUN npm ci --omit=dev
COPY proto/ ./proto/
COPY client/ ./client/
USER node
CMD ["node", "client/src/ai-demo.js"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Use native distro tools; bundled Grpc.Tools protoc crashes on some ARM64 hosts.
RUN apt-get update && apt-get install -y --no-install-recommends protobuf-compiler \
    && rm -rf /var/lib/apt/lists/*
COPY server/HumanResolve.ArgumentService/HumanResolve.ArgumentService.csproj server/HumanResolve.ArgumentService/
COPY proto/ proto/
RUN dotnet restore server/HumanResolve.ArgumentService/HumanResolve.ArgumentService.csproj
COPY server/HumanResolve.ArgumentService/ server/HumanResolve.ArgumentService/
RUN dotnet publish server/HumanResolve.ArgumentService/HumanResolve.ArgumentService.csproj --no-restore -c Release -o /out \
    -p:Protobuf_ProtocFullPath=/usr/bin/protoc

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS service
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /out/ ./
ENV GRPC_BIND_ANY=true GRPC_PORT=50051
EXPOSE 50051
USER $APP_UID
ENTRYPOINT ["dotnet", "HumanResolve.ArgumentService.dll"]
