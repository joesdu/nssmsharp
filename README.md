# NssmSharp

NssmSharp 是一个用纯 C#实现的 Windows 服务管理工具，旨在替代 nssm（Non-Sucking Service Manager），支持以服务方式管理任意可执行程序。并且完全使用 Github Copilot 生成,仅人工处理了一丢丢问题.
感兴趣的可以试试,我也还没测试过.😁 主要是为了测试目前 AI 的能力.

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

## 命令行参数

```bash
NssmSharp - 纯C# NSSM服务管理器

命令列表:
  install <服务名> <可执行文件路径> [参数]   安装新服务
  edit <服务名>                         编辑服务配置 (弹窗)
  remove <服务名> [confirm]              卸载服务，可加 confirm 跳过确认
  start <服务名>                         启动服务
  stop <服务名>                          停止服务
  restart <服务名>                       重启服务
  status <服务名>                        查询服务状态
  statuscode <服务名>                    查询服务状态码
  rotate <服务名>                        请求日志轮转
  list [all]                             列出 NssmSharp 管理的服务，all 显示全部
  processes <服务名>                     显示服务关联进程
  dump <服务名> [新服务名]               导出服务为 nssm 命令
  get <服务名> <参数名> [子参数]         获取服务参数
  set <服务名> <参数名> <值...>          设置服务参数
  reset <服务名> <参数名>                重置服务参数为默认
  unset <服务名> <参数名>                删除服务参数
  gui                                    启动图形界面
  help, -h, --help                       显示本帮助
```

## 贡献

欢迎提交 issue 和 PR。
