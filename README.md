# NssmSharp

NssmSharp 是一个用纯 C#实现的 Windows 服务管理工具，旨在替代 nssm（Non-Sucking Service Manager），支持以服务方式管理任意可执行程序。并且完全使用 Github Copilot 生成,仅人工处理了一丢丢问题.
感兴趣的可以试试,我也还没测试过.😁

## 功能特性

- 以 Windows 服务方式运行任意可执行文件
- 支持服务的安装、启动、停止、卸载
- 支持服务配置参数

## 快速开始

1. 使用 .NET 10.0 或更高版本编译本项目：
   ```pwsh
   dotnet build
   ```
2. 后续将实现命令行参数用于安装/卸载/启动/停止服务。

## 贡献

欢迎提交 issue 和 PR。
