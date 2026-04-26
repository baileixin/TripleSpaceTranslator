# TripleSpaceTranslator

TripleSpaceTranslator 是一个 Windows 10 轻量级托盘翻译工具：按下全局快捷键后，读取当前焦点输入框里的整段文本，调用机器翻译 API 翻译成设置中的目标语言，并尝试把翻译结果写回原输入框。

项目最初采用“三击空格”触发。为了避免和中文输入法、快速打字、浏览器输入框产生冲突，当前版本改为可自定义全局快捷键，默认 `Ctrl + Alt + Q`。

## 功能

- Windows 托盘常驻应用，基于 C#、.NET 8、WPF。
- 可自定义全局快捷键。
- 支持默认目标语言配置。
- 支持开机自启动开关。
- 支持翻译服务配置和“测试连接”。
- 优先通过 UI Automation 读取/写回输入框文本，必要时退化为剪贴板粘贴和键盘输入。
- SecretId、SecretKey 等密钥用 Windows DPAPI 按当前用户加密保存，不写入仓库。
- Core 层提供翻译服务抽象，不绑定单一厂商；腾讯云、百度、有道、阿里云、DeepL、Google、Azure 或类似机器翻译 API 都可以通过 Provider Adapter 接入。

## 当前内置翻译服务

当前代码内置的是腾讯云机器翻译 `TextTranslate` Provider。

项目结构不是腾讯云专用：

- `ITranslationProvider` 定义统一的翻译和连接测试接口。
- `TranslationProviderConfig` 保存 Provider 配置。
- `TranslationProviderFactory` 根据配置创建具体 Provider。
- 键盘监听、焦点输入框访问、写回逻辑、设置持久化、自启动注册都和具体翻译厂商解耦。

也就是说，类似的机器翻译 API 都可以按同一套接口接入。当前 UI 只暴露腾讯云字段；新增其它服务时，需要同步补 Provider、配置字段、设置界面和测试用例。

## 系统要求

- Windows 10 19041 或更高版本。
- .NET 8 SDK。
- 如果使用内置腾讯云 Provider，需要腾讯云机器翻译服务的 SecretId 和 SecretKey。

## 构建

使用系统 .NET SDK：

```powershell
dotnet restore
dotnet build .\src\TripleSpaceTranslator.App\TripleSpaceTranslator.App.csproj
```

使用本项目目录里的本地 SDK：

```powershell
.\.dotnet\dotnet.exe restore
.\.dotnet\dotnet.exe build .\src\TripleSpaceTranslator.App\TripleSpaceTranslator.App.csproj
```

## 运行

Debug 构建后的程序路径：

```powershell
.\src\TripleSpaceTranslator.App\bin\Debug\net8.0-windows10.0.19041.0\TripleSpaceTranslator.App.exe
```

启动后在系统托盘打开设置窗口，配置：

- 默认目标语言。
- 触发快捷键。
- 开机自启动。
- 翻译服务凭证。
- Region、ProjectId、超时时间。

“测试连接”会直接使用当前设置窗口里填写的配置，不需要先保存。

## 配置和密钥

运行时配置保存在：

```text
%LocalAppData%\TripleSpaceTranslator\settings.json
%LocalAppData%\TripleSpaceTranslator\secrets\
```

`settings.json` 只保存非敏感配置。翻译服务密钥单独保存在 `secrets` 目录，并使用 Windows DPAPI 按当前用户加密。

不要把真实 API Key、SecretId、SecretKey 提交到仓库。本仓库 `.gitignore` 已忽略常见本地密钥文件，例如 `.env`、`*.key`、`*.pem`、`settings.json`、`secrets/`。

## 接入新的翻译服务

新增一个机器翻译服务通常需要：

1. 在 `TranslationProviderType` 增加 Provider 类型。
2. 新建类实现 `ITranslationProvider`。
3. 在 `TranslationProviderFactory` 注册新 Provider。
4. 根据服务需要扩展 `TranslationProviderConfig` 或增加专用配置模型。
5. 在设置窗口增加对应配置项。
6. 增加单元测试，覆盖成功响应、鉴权失败、超时、网络异常、服务端错误和连接测试。

Provider 约定：

- `TranslateAsync` 只返回翻译后的文本。
- `TestConnectionAsync` 优先调用轻量健康检查；没有健康检查时，用最小化测试翻译请求验证连接。
- 超时、鉴权失败、额度不足、网络异常、服务端错误需要映射为用户可读的错误消息。

## 已知限制

首版目标是普通 Windows 输入框和浏览器 `input` / `textarea`。以下场景不保证写回成功：

- 密码框。
- 只读输入框。
- 管理员权限应用，而本应用未以管理员权限运行。
- IDE、富文本编辑器、自绘聊天框、canvas 编辑器。
- 不暴露标准 UI Automation 模式的控件。

遇到不支持的输入框时，应用应该安全失败并弹出非阻塞通知，不应崩溃。

## 测试

使用系统 .NET SDK：

```powershell
dotnet test .\tests\TripleSpaceTranslator.Tests\TripleSpaceTranslator.Tests.csproj
```

使用本项目目录里的本地 SDK：

```powershell
.\.dotnet\dotnet.exe test .\tests\TripleSpaceTranslator.Tests\TripleSpaceTranslator.Tests.csproj --disable-build-servers
```

## 开源协议

MIT
