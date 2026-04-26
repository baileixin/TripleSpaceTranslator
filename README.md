# TripleSpaceTranslator

TripleSpaceTranslator 是一个面向 Windows 10 的轻量级托盘翻译工具。按下全局快捷键后，程序会读取当前输入框里的整段文字，调用机器翻译 API 翻译成你设置的目标语言，再尽量把结果写回原输入框。

项目最初尝试过“三击空格触发”，但这种方式容易和中文输入法、连续打字、浏览器输入框冲突。当前版本已经改为可自定义全局快捷键，默认是 `Ctrl + Alt + Q`。

## 现在能做什么

- 托盘常驻，支持 Windows 10
- 支持自定义全局快捷键
- 支持设置默认目标语言
- 支持开机自启
- 支持“测试连接”，不用先保存
- 支持在设置里切换翻译服务
- 当前内置支持：
  - 腾讯云机器翻译 `TextTranslate`
  - 百度通用文本翻译

## 面向非技术用户的配置方式

设置页已经按“尽量少填参数”做了调整：

- 选择翻译服务
- 填入对应的 `ID` 和 `Key`
- 点 `测试连接`
- 成功后保存

对大多数用户来说：

- 使用百度时，通常只需要 `AppId` 和 `AppKey`
- 使用腾讯云时，通常只需要 `SecretId` 和 `SecretKey`

腾讯云的 `Region` 和 `ProjectId` 仍然支持，但默认被收进“高级设置”里：

- `Region` 默认 `ap-guangzhou`
- `ProjectId` 默认 `0`

也就是说，普通用户切换服务时，不需要先理解一堆云厂商术语，通常改两项凭证就够了。

## 翻译服务架构

这个项目不是只绑死某一家翻译厂商。

- `ITranslationProvider` 统一定义翻译和连接测试接口
- `TranslationProviderFactory` 负责按配置创建具体 Provider
- 设置窗口会根据不同 Provider 动态显示对应字段标签
- 腾讯云和百度只是当前内置的两个实现

如果后面要接入有道、阿里云、Azure、Google、DeepL 或其他同类机器翻译服务，整体架构已经支持继续扩展。

## 系统要求

- Windows 10 19041 或更高
- .NET 8 SDK

## 构建

使用系统安装的 .NET SDK：

```powershell
dotnet restore
dotnet build .\src\TripleSpaceTranslator.App\TripleSpaceTranslator.App.csproj
```

如果使用仓库里自带的本地 SDK：

```powershell
.\.dotnet\dotnet.exe restore
.\.dotnet\dotnet.exe build .\src\TripleSpaceTranslator.App\TripleSpaceTranslator.App.csproj
```

## 运行

Debug 输出路径：

```powershell
.\src\TripleSpaceTranslator.App\bin\Debug\net8.0-windows10.0.19041.0\TripleSpaceTranslator.App.exe
```

启动后，从托盘打开设置窗口，配置：

- 默认目标语言
- 全局快捷键
- 开机自启
- 翻译服务
- API 凭证
- 连接测试

## 配置和密钥保存

程序配置保存在：

```text
%LocalAppData%\TripleSpaceTranslator\settings.json
%LocalAppData%\TripleSpaceTranslator\secrets\
```

- `settings.json` 只保存普通配置
- 凭证单独保存在 `secrets` 目录
- 凭证使用 Windows DPAPI 按当前用户加密

不要把真实 API Key、SecretId、SecretKey、AppKey 提交到仓库里。

## 已知限制

首版重点支持标准输入框和浏览器 `input` / `textarea`。以下场景不保证写回成功：

- 密码框
- 只读输入框
- 管理员权限应用
- IDE
- 富文本编辑器
- 自绘聊天框
- 不暴露标准 UI Automation 模式的控件

## 测试

```powershell
.\.dotnet\dotnet.exe test .\tests\TripleSpaceTranslator.Tests\TripleSpaceTranslator.Tests.csproj --disable-build-servers
```

## 参考文档

- [百度翻译开放平台 - 通用文本翻译 API](https://fanyi-api.baidu.com/product/113)
- [百度翻译开放平台 - API 说明](https://fanyi-api.baidu.com/api/trans/product/prodinfo)
- [腾讯云机器翻译 TextTranslate](https://cloud.tencent.com/document/product/551/15619)

## License

MIT
