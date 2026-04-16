# Octokit.Net-AOT

## 项目简介

Octokit.Net-AOT 是一个为 GitHub API 客户端库 Octokit.Net 提供 AOT（Ahead-Of-Time）编译支持的项目。该项目通过集成 [RS.SimpleJson-Unity](https://github.com/RS-Unity3D/RS.SimpleJson-Unity) 库，解决了 Octokit.Net 在 Unity IL2CPP 和 NativeAOT 构建中的 AOT 兼容性问题。

## 主要特性

- ✅ **AOT 兼容**：完全支持 Unity IL2CPP 和 .NET NativeAOT 编译
- ✅ **最小修改**：保留原始 Octokit.Net 代码，通过条件编译切换实现
- ✅ **高性能**：使用 RS.SimpleJson-Unity 的优化反射缓存
- ✅ **跨平台**：支持所有 Unity 平台（包括 WebGL）
- ✅ **向后兼容**：非 AOT 环境下使用原始 SimpleJson 实现
- ✅ **独立封装**：AOT 相关代码独立封装在 `OctokitAOT` 文件夹中

## 安装说明

### 1. 克隆项目

```bash
git clone https://github.com/yourusername/Octokit.Net-AOT.git
```

### 2. 依赖项

- **RS.SimpleJson-Unity**：AOT 友好的 JSON 序列化库
  - 已内置在 `RS.SimpleJson-Unity-DLL` 目录中
  - 版本：v2.2.0+

### 3. 在 Unity 项目中使用

1. 将 `Octokit` 目录复制到 Unity 项目的 `Assets/Plugins` 文件夹中
2. 确保 `RS.SimpleJson-Unity.dll` 也被复制到同一目录
3. 在 `PlayerSettings` 中添加编译符号：`USE_AOT_JSON`

### 4. 在 .NET 项目中使用

1. 在项目中引用 `Octokit` 项目
2. 确保引用 `RS.SimpleJson-Unity.dll`
3. 在项目文件中添加条件编译符号：`USE_AOT_JSON`

## 使用方法

### 基本使用

```csharp
using Octokit;

// 创建 GitHub 客户端
var client = new GitHubClient(new ProductHeaderValue("YourAppName"))
{
    Credentials = new Credentials("your-token")
};

// 获取仓库信息
var repo = await client.Repository.Get("octokit", "octokit.net");
Console.WriteLine($"仓库名称: {repo.FullName}");
```

### AOT 模式配置

**启用 AOT 模式**：
- **Unity 项目**：在 `PlayerSettings` → `Scripting Define Symbols` 中添加 `USE_AOT_JSON`
- **.NET 项目**：在项目文件的 `<DefineConstants>` 中添加 `USE_AOT_JSON`

**禁用 AOT 模式**（使用原始实现）：
- 从 `Scripting Define Symbols` 或项目文件中移除 `USE_AOT_JSON`

### NativeAOT 编译

```bash
# 发布 AOT 可执行文件
dotnet publish -c Release -r win-x64 --self-contained true
```

### 初始化 AOT 类型

在应用启动时初始化常用 AOT 类型：

```csharp
// 初始化 RS.SimpleJson-Unity 的 AOT 类型
RS.SimpleJsonUnity.SimpleJson.InitializeCommonAotTypes();
```

## 项目结构

```
Octokit.Net-AOT/
├── Octokit/                      # 核心代码
│   ├── Octokit.csproj            # 项目文件（包含 USE_AOT_JSON 符号）
│   ├── SimpleJson.cs             # 原始 SimpleJson 实现（保留）
│   ├── Http/
│   │   └── SimpleJsonSerializer.cs # 非AOT 版本（#if !USE_AOT_JSON）
│   ├── OctokitAOT/             # AOT 专用代码
│   │   ├── SimpleJsonSerializer.cs      # AOT 版本（#if USE_AOT_JSON）
│   │   ├── SimpleJsonAOT.cs          # AOT 序列化策略
│   │   └── Octokit.Reflection.cs      # AOT 反射工具类
│   ├── Helpers/                # 辅助类
│   ├── Models/                 # 数据模型
│   └── ...
├── RS.SimpleJson-Unity-DLL/     # RS.SimpleJson-Unity 库
├── Octokit.Tests.AOT/          # AOT 测试项目
│   ├── Octokit.Tests.AOT.csproj  # AOT 测试项目
│   └── Program.cs               # 测试代码
└── README.md                    # 本说明文档
```

## 核心实现

### AOT 桥接适配

项目通过独立的 `OctokitAOT` 文件夹实现了 AOT 支持：

#### 1. **SimpleJsonSerializer.cs**（AOT 版本）
- 实现 `IJsonSerializer` 接口
- 使用 `RS.SimpleJsonUnity.SimpleJson` 进行序列化
- 条件编译：`#if USE_AOT_JSON`

#### 2. **SimpleJsonAOT.cs**（AOT 序列化策略）
- 继承 `RS.SimpleJsonUnity.DefaultJsonSerializationStrategy`
- 实现自定义序列化逻辑
- 复用 Octokit 现有的反射工具

#### 3. **Octokit.Reflection.cs**（AOT 反射工具）
- 提供与原始 `ReflectionUtils` 兼容的接口
- 支持条件编译和 AOT 优化

### 条件编译切换

```csharp
// Http/SimpleJsonSerializer.cs
#if !USE_AOT_JSON
    public class SimpleJsonSerializer : IJsonSerializer
    {
        // 原始实现
    }
#endif

// OctokitAOT/SimpleJsonSerializer.cs
#if USE_AOT_JSON
    public class SimpleJsonSerializer : IJsonSerializer
    {
        // AOT 实现
    }
#endif
```

## 测试验证

### 运行测试

```bash
# 运行 AOT 模式测试
dotnet run --project Octokit.Tests.AOT/Octokit.Tests.AOT.csproj

# 发布 AOT 可执行文件并测试
dotnet publish Octokit.Tests.AOT/Octokit.Tests.AOT.csproj -c Release -r win-x64
./bin/Release/net9.0/win-x64/publish/Octokit.Tests.AOT.exe
```

### 测试内容

1. **JSON 序列化/反序列化**：测试基本对象的序列化和反序列化
2. **复杂对象**：测试包含嵌套结构的复杂对象
3. **列表类型**：测试数组和列表的序列化
4. **枚举处理**：测试自定义枚举的序列化
5. **GitHub API 调用**：测试实际的 GitHub API 调用

## 性能优化

1. **反射缓存**：使用 `ConcurrentDictionary` 缓存反射信息
2. **字符串处理**：使用 `StringBuilder` 减少内存分配
3. **类型转换**：优化基本类型和复杂类型的转换
4. **循环引用检测**：避免序列化时的无限递归
5. **线程安全**：使用并发集合支持多线程访问

## 已知限制

- **API 兼容性**：RS.SimpleJson-Unity 与原始 SimpleJson 的 API 不完全兼容，需要适配器桥接
- **WebGL 平台**：需要确保使用 `SIMPLE_JSON_WEBGL` 编译符号启用无锁模式
- **复杂对象**：某些复杂的嵌套对象可能需要手动处理
- **Expression Trees**：AOT 环境下不支持动态 Expression Tree 生成

## 技术细节

### AOT 模式工作流程

```
┌─────────────────────────────────────────────────────────────┐
│                    SimpleJsonSerializer                  │
├─────────────────────────────────────────────────────────────┤
│  #if USE_AOT_JSON                                   │
│    → RS.SimpleJsonUnity.SimpleJson                  │
│    → GitHubJsonSerializerStrategy                      │
│    → Octokit.Reflection (AOT 版本)                   │
│  #else                                              │
│    → Octokit.SimpleJson                             │
│    → GitHubSerializerStrategy                         │
│    → Octokit.Reflection (原始版本)                       │
│  #endif                                             │
└─────────────────────────────────────────────────────────────┘
```

### 关键技术点

1. **条件编译**：使用 `#if USE_AOT_JSON` 在编译时选择实现
2. **接口兼容**：两个版本实现相同的 `IJsonSerializer` 接口
3. **代码复用**：AOT 版本复用 Octokit 现有的反射逻辑
4. **性能优化**：缓存反射结果，减少运行时开销

## 贡献指南

1. **提交 Issues**：报告 bug 或提出功能建议
2. **Pull Requests**：提交代码改进
3. **测试**：添加新的测试用例
4. **文档**：改进文档和示例

## 版本历史

### v1.0.0 (2026-04-16)
- 初始版本发布
- 支持 Unity IL2CPP 和 .NET NativeAOT
- 集成 RS.SimpleJson-Unity 库
- 独立的 AOT 代码封装

## 许可证

本项目基于 MIT 许可证，与原始 Octokit.Net 保持一致。

## 相关项目

- [Octokit.Net](https://github.com/octokit/octokit.net)：GitHub API 客户端库
- [RS.SimpleJson-Unity](https://github.com/RS-Unity3D/RS.SimpleJson-Unity)：AOT 友好的 JSON 序列化库

## 联系方式

如有问题或建议，请通过 GitHub Issues 联系我们。

---

**版本**: 1.0.0  
**发布日期**: 2026-04-16  
**兼容平台**: Unity 2019.4+（IL2CPP）、.NET Standard 2.0+、.NET NativeAOT
**目标框架**: .NET Standard 2.0, .NET 9.0+
