# 内网穿透设置指南

## 方案1: 使用ngrok

### 安装ngrok
1. 访问 https://ngrok.com/download 下载ngrok
2. 解压到您的系统PATH中
3. 注册账号并获取authtoken
4. 运行 `ngrok authtoken YOUR_AUTHTOKEN`

### 使用方法
```bash
# 启动您的ASP.NET Core应用（通常在端口5000或5001）
dotnet run

# 在另一个终端中启动ngrok
ngrok http 5001
```

### 配置文件 (ngrok.yml)
```yaml
version: "2"
authtoken: YOUR_AUTHTOKEN
tunnels:
  web:
    addr: 5001
    proto: http
    hostname: your-subdomain.ngrok.io  # 需要付费计划
```

## 方案2: 使用Visual Studio Code内置转发

如果使用VS Code Remote功能：
1. 打开命令面板 (Ctrl+Shift+P)
2. 搜索 "Forward a Port"
3. 输入端口号 (如5001)
4. VS Code会自动创建一个公共URL

## 方案3: 使用其他免费工具

### localtunnel
```bash
npm install -g localtunnel
lt --port 5001
```

### serveo
```bash
ssh -R 80:localhost:5001 serveo.net
```

## 安全注意事项

⚠️ **重要提醒**：
- 只在开发和测试时使用内网穿透
- 不要暴露生产环境或敏感数据
- 考虑添加身份验证
- 定期更换访问URL
- 监控访问日志

## 为您的ASP.NET Core项目配置

在您的项目中，确保配置了正确的CORS和HTTPS重定向策略：

```csharp
// 在Program.cs中添加
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNgrok", policy =>
    {
        policy.SetIsOriginAllowed(origin => 
               Uri.TryCreate(origin, UriKind.Absolute, out var uri) && 
               (uri.Host == "localhost" || uri.Host.EndsWith(".ngrok.io")))
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 在Configure中使用
app.UseCors("AllowNgrok");
```
