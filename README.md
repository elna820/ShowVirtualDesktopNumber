## 中文介绍

ShowVirtualDesktopNumber 是一款轻量级 Windows 11 托盘小工具，用于在系统托盘图标上实时显示当前虚拟桌面的编号。程序启动后不会显示主窗口，只在托盘后台运行；右键托盘图标可以查看版本号、切换语言并退出程序。
这是一款绿色软件，直接运行即可使用，不需要安装，不写注册表。软件支持中文和英文，语言设置会保存到同目录下的 config.toml 配置文件中。程序支持单实例运行，重复打开不会生成多个托盘图标。

## English Introduction

ShowVirtualDesktopNumber is a lightweight Windows 11 system tray utility that displays the current virtual desktop number directly on the tray icon. After launch, it runs silently in the background without opening a main window. Right-clicking the tray icon lets you view the version, switch languages, and exit the app.
It is a portable/green utility: no installation is required, and it does not write to the Windows Registry. The app supports both Chinese and English, with the language preference stored in the local config.toml file. It also supports single-instance execution, so launching it repeatedly will not create duplicate tray icons.

## 运行环境

这个可执行文件当前是用 .NET Framework 4.x 的 WinForms 编译的，运行环境要求很轻：<br>
操作系统：Windows 11<br>
架构：x64<br>
运行时：.NET Framework 4.x，通常 Windows 10/11 已自带或可通过系统功能/Windows Update 获得<br>
权限：普通用户权限即可，不需要管理员权限<br>
依赖：无第三方依赖<br>
桌面环境：需要 Windows Explorer/System Tray 正常运行<br>
虚拟桌面编号读取：依赖当前用户注册表中的 Windows 虚拟桌面状态信息<br>
项目当前用的是系统自带编译器路径：C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe<br>
所以生成的 exe 不是 .NET 8 独立程序，也不需要安装 .NET 8 Runtime。<br>

## Runtime Requirements

Operating system: Windows 11<br>
Architecture: x64<br>
Runtime: .NET Framework 4.x<br>
Permissions: Standard user permissions are enough; administrator rights are not required<br>
Dependencies: No third-party dependencies<br>
Desktop shell: Windows Explorer and the system tray must be running normally<br>
Virtual desktop detection: Reads Windows virtual desktop state from the current user registry Build Environment <br>
The project is currently compiled with the built-in .NET Framework C# compiler:<br>
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe <br>
The generated executable is a .NET Framework WinForms application. It is not a .NET 8 self-contained app and does not require the .NET 8 Runtime.<br>

