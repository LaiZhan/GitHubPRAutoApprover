# podman build -f GitHubPRAutoApprover.Web/Dockerfile -t registry.cn-shanghai.aliyuncs.com/laizhan/githubprautoapprover:1.0 .
# podman-compose up -d
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["GitHubPRAutoApprover.Web/GitHubPRAutoApprover.Web.csproj", "GitHubPRAutoApprover.Web/"]
RUN dotnet restore "GitHubPRAutoApprover.Web/GitHubPRAutoApprover.Web.csproj"
COPY . .
WORKDIR "/src/GitHubPRAutoApprover.Web"
RUN dotnet build "./GitHubPRAutoApprover.Web.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./GitHubPRAutoApprover.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# 已移除时区相关设置
# ENV TZ=Asia/Shanghai
# RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone
# ENV ASPNETCORE_DATAPROTECTION__KEYDIRECTORY=/app/.aspnet/DataProtection-Keys
ENTRYPOINT ["dotnet", "GitHubPRAutoApprover.Web.dll"]