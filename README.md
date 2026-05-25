# ReSeq

ReSeq 是一个 Windows 原生 WPF 桌面软件，用来按 `分镜号-版本号` 管理短剧视频文件。

它不是表单式批量重命名工具。用户选择视频文件夹后，软件会把 `1-1.mp4`、`1-2.mp4`、`2-1.mp4` 这类文件放进缩略图网格：行是 `X`，列是 `Y`。拖入新视频后，ReSeq 会根据投放位置生成重命名预览，确认后再执行安全重命名。

## 功能

- 选择文件夹并扫描视频
- 只管理严格符合 `数字-数字.扩展名` 的视频文件
- 支持 `.mp4`、`.mov`、`.avi`、`.mkv`、`.wmv`
- 以缩略图网格显示视频
- 从 Windows 文件管理器拖入单个新视频
- 拖到行之间：插入新镜头
- 拖到同一行版本之间：插入新版本
- 拖到空单元格：直接命名为该 `X-Y`
- 拖到已有视频：选择放到前面或后面
- 执行前显示完整预览
- 使用两阶段临时改名，避免覆盖文件
- 记录非法文件、重复编号、临时文件残留和执行结果

## 项目结构

```text
ReSeq.slnx
src/
  ReSeq/          WPF 图形界面、拖拽交互、缩略图加载
  ReSeq.Core/     扫描、计划、安全重命名等核心逻辑
tests/
  ReSeq.Tests/    无外部依赖的控制台测试
```

## 运行

```powershell
dotnet run --project .\src\ReSeq\ReSeq.csproj
```

## 测试

```powershell
dotnet run --project .\tests\ReSeq.Tests\ReSeq.Tests.csproj
```

测试覆盖：

- 插入新镜头行
- 插入同镜头新版本
- 拖到空单元格

## 打包成 exe

生成框架依赖的单文件 exe：

```powershell
dotnet publish .\src\ReSeq\ReSeq.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

输出目录：

```text
src\ReSeq\bin\Release\net8.0-windows\win-x64\publish\
```

如果要给没有安装 .NET 8 Desktop Runtime 的机器使用，可以改成自包含：

```powershell
dotnet publish .\src\ReSeq\ReSeq.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## 重命名安全策略

执行前先生成 `RenamePlan`，只展示预览，不改文件。

确认执行后：

1. 需要移动的旧文件先改成 `__temp_rename_{guid}.扩展名`
2. 再从临时文件改成最终目标名
3. 新拖入的视频最后进入目标位置
4. 目标文件如果已存在且不属于本次计划，会阻止执行
5. 失败时尽量回滚，并把结果写入日志

## 示例

原文件：

```text
1-1.mp4
2-1.mp4
2-2.mp4
3-1.mp4
```

把 `new.mp4` 拖到 `X=2` 之前，预期：

```text
1-1.mp4
2-1.mp4  new.mp4
3-1.mp4  原 2-1
3-2.mp4  原 2-2
4-1.mp4  原 3-1
```
