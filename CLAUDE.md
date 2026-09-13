# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 語言偏好
- 請全程使用「繁體中文」與使用者溝通及回答所有技術問題。

## 角色設定
你是資深 .NET 8 架構師，除了開發 C# WPF 外，也負責將 Python 系統重構為 C# WPF。請嚴格執行本文件「架構規則摘要」章節的所有規範。在重構複雜模組時，請先確認型別對應正確（可參考 `03_GLOSSARY_AND_MAPPINGS.md`），再開始撰寫程式碼。

## Project overview

SerialController is a layered .NET 8 library for controlling arbitrary Modbus RTU/ASCII slave devices over a serial port (RS-232/RS-485). It's one domain-specific repo in a family of sibling solutions that all follow the same `Abstractions → Core → Protocols → TestApp` layering described below (SwallowBase_DLL, AdamIoController, RobotController, VisionController, EIBG_Assemble — separate repos, not present here).

Unlike `AdamIoController` (Modbus **TCP**, tied to one device family — Advantech ADAM-6200), this repo isn't tied to a single device model: `IModbusSerialDevice` exposes generic Modbus function-code operations (coils, discrete inputs, holding/input registers), so any consumer can drive whatever Modbus slave is wired up over whatever serial transport. `EIBG_Assemble` is the intended eventual consumer, the same way it consumes `AdamIoController` (a consuming app's own `*Coordinator` would load a JSON-backed device list, build the registry with the concrete `ModbusSerialDeviceFactory`, and own the panel view model) — nothing there references this repo yet.

## Build & test

- Build: `dotnet build SerialController.sln -c Debug`
- Solution only defines `Debug|x64` / `Release|x64` — there is no AnyCPU configuration.
- There is currently no automated test project (no xUnit project in the solution) and, unlike `AdamIoController`, no `TestApp.Console` either — skipped for this pass, see the 待辦事項 in `HANDOFF_SUMMARY.md` for what a follow-up manual verification tool should look like.
- No physical serial hardware was available while building this: `dotnet build SerialController.sln -c Debug|Release` succeeds cleanly (0 warnings, 0 errors) across all four projects, but NModbus RTU/ASCII framing behavior against a real device is completely unverified.
- **`SerialController.Wpf.Controls` references a sibling repo by relative path**: `..\..\SwallowBase\SwallowBase_DLL\SwallowBase_DLL.csproj`. That repo must be checked out alongside this one (as a sibling directory of `SerialController`) or the solution won't build.

## Solution layout

Four projects, matching the layered architecture in "架構規則摘要" below:

- `SerialController.Abstractions` — bottom layer. Two independent contracts, both in `Interfaces/`: `ISerialPortConnection`/`ISerialPortConnectionFactory` (generic raw serial byte transport — open/close/read/write, not Modbus-specific, exists so a future non-Modbus serial protocol has something to reuse) and `IModbusSerialDevice`/`IModbusSerialDeviceFactory` (the actual device contract — generic Modbus function-code reads/writes: coils, discrete inputs, holding/input registers). `Models/`: `SerialPortConfig` (port name, baud rate, data bits, stop bits, parity, timeouts) and `ModbusSerialDeviceConfig` (wraps a `SerialPortConfig` + slave ID + `ModbusProtocolMode`), both `readonly record struct`s with a `CreateDefault(...)` factory method. No protocol/connection logic.
- `SerialController.Core` — `ModbusSerialDeviceRegistry`/`SerialPortConnectionRegistry`: manage the dynamic (not hardcoded-count) lists of configured devices/connections, depend only on `Abstractions` (build devices via the injected `IModbusSerialDeviceFactory`/`ISerialPortConnectionFactory`, never a concrete protocol implementation). Both are thin subclasses of `DeviceRegistry<TDevice>` (added 2026-09-14 — see 待辦/HANDOFF_SUMMARY §2026-09-14), which holds the shared "named list, connect-all-in-order, dispose-before-remove" behavior; each subclass keeps its own public API shape/naming (`Devices`/`Connections`, `AddDevice`/`AddConnection`, etc.) unchanged. Structurally mirrors `AdamIoController.Core.AdamIoModuleRegistry`.
- `SerialController.Protocols.NModbus` — `ModbusSerialDevice`/`ModbusSerialDeviceFactory`, the Modbus RTU/ASCII implementation using the `NModbus` package; `SerialPortStreamResource`, a hand-written `IStreamResource` adapter NModbus itself doesn't ship for serial ports (see design decisions below); `SerialPortConnection`/`SerialPortConnectionFactory`, a plain `ISerialPortConnection` wrapper over `System.IO.Ports.SerialPort` that's independent of the Modbus device (see design decisions for why both live in one project); and `SerialPortFactory` (added 2026-09-14, `internal`), the shared `SerialPort` construction/open/Parity-StopBits-mapping logic both `ModbusSerialDevice` and `SerialPortConnection` call into instead of duplicating it.
- `SerialController.Wpf.Controls` — reusable panel/view models (`SerialDevicePanelView`/`ViewModel`, `ModbusSerialDeviceViewModel`, `RegisterViewModel`) meant for consuming apps like `EIBG_Assemble`. Currently also builds as a runnable `WinExe` (`App.xaml`/`MainWindow.xaml`) for ad hoc testing, but stays blank scaffolding (theme merge only) — no wiring to real devices yet, matching `AdamIoController.Wpf.Controls`'s actual current state. Depends on `Abstractions` + `Core` + the sibling `SwallowBase_DLL` (theme resources) — **not** `Protocols.NModbus`, per the layering rule that a `*.Wpf.Controls` library must not know which concrete protocol implementation is in use.

## Key design decisions worth knowing before changing this code

- **NModbus 3.0.83 has no built-in serial-port adapter.** Confirmed by reflecting over the actual installed package, not assumed: it ships TCP/UDP/Socket adapters (`TcpClientAdapter`/`UdpClientAdapter`/`SocketAdapter`) but nothing for `System.IO.Ports.SerialPort`. `SerialPortStreamResource` fills this gap by implementing NModbus's `IStreamResource` directly. It deliberately does **not** own the `SerialPort`'s lifecycle (its `Dispose()` is a no-op) — `ModbusSerialDevice.Disconnect()` owns opening/closing the port.
- **Master creation goes through `ModbusFactory.CreateRtuMaster`/`CreateAsciiMaster`**, which are extension methods in the `NModbus` namespace (`NModbus.FactoryExtensions`), not instance methods on `ModbusFactory` itself — easy to miss since `ModbusFactory`/`IModbusFactory` only expose `CreateMaster(TcpClient|UdpClient|Socket|IModbusSerialTransport)` directly.
- **`ConnectAsync`/`OpenAsync` return `bool`, never throw.** Connection failure (wrong port name, port already in use, device not powered) is a normal, expected outcome in field deployment, not an exceptional one — same rationale as `AdamIoController`'s `IAdamIoModule.ConnectAsync`. The actual exception message is captured in `LastError` for diagnostics only; callers must not parse its exact wording.
- **`ModbusSerialDevice` serializes all master access through a `SemaphoreSlim`**, same pattern and same reason as `AdamIoController.Protocols.Modbus.ModbusAdamIoModule._modbusLock`: Modbus (RTU/ASCII included) is strictly request/response — a poll timer and a manual write hitting the same master concurrently silently misalign request/response frames without a lock, with no exception thrown. Any new caller path added to this class must also go through the lock.
- **Known limitation, not yet solved**: each `ModbusSerialDevice` opens its own `SerialPort` exclusively. Multiple slave IDs sharing one physical RS-485 line through a single COM port isn't supported by this v1 — that needs a shared-connection redesign, not attempted here since there's no known current requirement for it.
- **`Protocols.NModbus` intentionally hosts two independent capabilities** — the raw `ISerialPortConnection` wrapper and the Modbus-specific `ModbusSerialDevice` — instead of splitting a `SerialController.Common` project out for the non-Modbus-specific parts. Mirrors `AdamIoController`'s explicit "no separate `Common` layer yet" call (see 架構規則摘要 below): revisit only if/when a second, non-Modbus serial protocol implementation actually needs to reuse the raw connection wrapper.
- **500ms is the established polling interval** for IO panels in this project family (matches `AdamIoController.Wpf.Controls`/`RobotController.Wpf.Controls`'s IO panels) — keep new polling code at this cadence rather than picking a different number.
- **`SerialDevicePanelViewModel.StatusMessage` is plain text, not theme-colored.** The ADAM version's `ConnectionSummaryState`→brush-converter machinery depends on `Brush-AdamIoPanelView-*` resource keys defined in the sibling `SwallowBase_DLL` repo's `Color.xaml`; this repo has no equivalent keys yet, so it wasn't built here — add the same mechanism only after the corresponding theme keys exist.
- Traditional Chinese XML doc comments throughout record *why*, often referencing the actual reflected NModbus API shape or a specific concurrency bug already fixed in the ADAM sibling — read them before changing behavior in `ModbusSerialDevice`, `SerialPortStreamResource`, and the `Wpf.Controls` view models, and keep them updated.

---

## 架構規則摘要（濃縮自雲端 Project `00_PROJECT_OVERVIEW.md` Part 1，跨專案通用）

> 這節是跨所有 .NET 重構專案的通用分層規則，不是 SerialController 專屬。若規則本身有變動，記得雲端 Project 的 `00_PROJECT_OVERVIEW.md` 也要同步更新。

### 分層定義

任何一個「領域」（domain，例如 Serial / Ethernet / Robot / Vision 等）都應拆成以下五種專案類型：

| 層級 | 專案命名慣例 | 職責 | 可以引用 | 不可引用 |
|---|---|---|---|---|
| Abstractions | `<Domain>.Abstractions` | 定義介面與共用資料模型 | （無，最底層） | Core / Protocols / TestApp |
| Common | `<Domain>.Common` | 領域共用設定、常數、非業務型別 | Abstractions | Core / Protocols / TestApp |
| Core | `<Domain>.Core` | 通訊 / 演算法核心邏輯，不含特定裝置或協定細節 | Abstractions, Common | Protocols / TestApp / UI |
| Protocols | `<Domain>.Protocols.<Impl>` | 特定裝置、協定、演算法版本的實作 | Abstractions, Common, Core | TestApp / UI |
| TestApp | `<Domain>.TestApp.<Console\|Wpf>` | 驗證與人工測試入口 | 上述所有層 | （無限制） |

**依賴方向鐵律：上層可以引用下層，下層絕不可反向引用上層。**

SerialController 目前沒有獨立的 `Common` 層（`Protocols.NModbus` 專案裡的 `SerialPortConnection`/`SerialPortStreamResource` 暫時跟 Modbus 實作放在一起），這是刻意的精簡，不是遺漏——只有在真的出現「跨 Core/Protocols/TestApp 共用、但不屬於介面契約」的型別時才需要拆出 `SerialController.Common`。

### 命名字尾慣例

`*Options`（設定）、`*Device`（裝置實作）、`*Loader`（設定載入）、`*Validator`（驗證）、`*Client`（通訊 client）、`*Session`（UI 層整合封裝）、`*Catalog`（清單定義）、`*ViewModel`、`*Command`（ICommand 實作）、`<Domain>.Wpf.Controls`（可跨應用程式引用的 WPF 元件庫）。

### 放置規則速查

新增介面 → `Abstractions`；新增核心邏輯 → `Core`；新增特定裝置/協定 → `Protocols.<Impl>`；新增跨層共用設定 → `Common`；新增人工驗證 UI → `TestApp.Wpf` / `TestApp.Console`。

### MVVM / WPF 規則

1. UI binding 屬性名稱一經建立視為對外契約，避免任意改名（會破壞 XAML 綁定）。
2. `ViewModel` 可整合多個領域的狀態與命令，但底層邏輯（連線、演算法、資料處理）不應寫在 ViewModel 裡，只能呼叫 `Core` / `Protocols` / `Service`。
3. 欄位語意相同時優先合併，不要為了方便平行建立語意相近的欄位。
4. 需即時更新 UI 的資料一律透過 `Dispatcher.Invoke`/`BeginInvoke` 或 `IProgress<T>` 回到 UI 執行緒，不可在背景執行緒直接操作 UI 元素或綁定的 `ObservableCollection`。
5. `ObservableObject` 屬於「資料模型的通知能力」，不算 UI 邏輯，`Common` 層模型也可以繼承它。

**可重用 UI 元件庫（`<Domain>.Wpf.Controls`）**：當 UI 元件需要被多個獨立應用程式引用時另開此專案，只能依賴 `Abstractions`/`Common`，絕不可依賴 `Protocols.<Impl>`（元件庫不應知道底層是哪個廠牌/協定的實作）。硬體相關的視覺化常數（連桿長度、工作半徑等）須集中放獨立檔案，並在註解中標明哪些是官方確認值、哪些是概估值。

### Python → C# 轉換對照

| Python 端 | C# / .NET 對應 | 備註 |
|---|---|---|
| `numpy` 陣列運算 | `System.Numerics` / `Math.NET Numerics` / 陣列 + `Span<T>` | 效能不足時考慮 `Parallel.For` |
| `cv2`（OpenCV） | `OpenCvSharp`（`OpenCvSharp4`） | 注意 Mat 記憶體釋放（`using`/`Dispose`） |
| `threading`/`multiprocessing` | `Task`/`System.Threading.Channels` | WPF 避免直接用 `Thread`，優先 `Task.Run` + `Dispatcher` |
| Python `dict`/動態屬性 | 明確定義的 `class`/`record`（`*Options`/`*Model`） | 不要用 `dynamic` 模擬彈性 |
| `json` 設定檔 | `System.Text.Json` | 對應 `*Loader`/`*Validator` |
| 寬鬆 try/except | 明確例外型別 + 自訂 Exception class | 避免整個方法包 `catch (Exception)` |
| 硬體 SDK（相機/PLC/控制卡） | 對應廠牌 .NET SDK | **必須向使用者索取 C# SDK 文件，不可憑空猜測 API** |
| 全域變數/單例 | DI 容器管理的 Singleton，或 `*Session` 封裝 | 避免用 `static` 硬翻譯 |

**轉換工作流程**：先確認領域歸屬 → 依 `Abstractions → Core → Protocols` 順序逐模組轉換 → 每完成一個模組回頭核對命名與依賴方向 → 效能敏感或有經驗值（threshold、ROI 座標）的邏輯必須用實際測試資料驗收。

### 給 AI 助手的提示

1. 先判斷程式碼屬於哪個領域、哪個層級。
2. 不要把 UI 邏輯混進 Core 或 Protocols；不要把通訊/硬體實作放進純資料模型類別。
3. 不確定 Python 邏輯該放哪一層時，先詢問使用者，不要自行假設。
4. 多個領域有相同行為模式時，優先維持命名與結構一致。
5. 欄位/屬性用途與既有相同時優先合併，不要平行建立語意相近的新欄位。

---

## Python → C# 轉換範例

**Python 字典 → C# record struct**

```python
pose_data = {"x": 10.5, "y": 20.0, "z": 0.0, "status": "Ready"}
```
```csharp
public readonly record struct CartesianPose(double X, double Y, double Z, string Status);
```

**純運算模組（CPU-bound）→ 同步方法**

```python
def calculate_distance(p1, p2):
    return ((p1.x - p2.x)**2 + (p1.y - p2.y)**2)**0.5
```
```csharp
public static double CalculateDistance(Point p1, Point p2)
{
    double dx = p1.X - p2.X;
    double dy = p1.Y - p2.Y;
    return Math.Sqrt(dx * dx + dy * dy);
}
```

**硬體 I/O 模組（I/O-bound）→ 非同步方法**

```python
def move_robot(x, y):
    device.send(f"MOVE {x} {y}")
    return device.read_response()
```
```csharp
public async Task<string> MoveRobotAsync(double x, double y)
{
    await _device.SendAsync($"MOVE {x} {y}");
    return await _device.ReadResponseAsync();
}
```

---

## 文件分工說明

- **本機 `CLAUDE.md`**（本文件）：SerialController 專屬說明 + 跨專案通用的架構規則/轉換範例濃縮版，供離開雲端 Project 也能獨立運作。
- **本機 `01_RULES_CODING_STANDARDS.md` / `03_GLOSSARY_AND_MAPPINGS.md`**：小專案適用的 C#/.NET 8 重構守則與 Python→C# 術語對照表，已同步放在這個 repo 根目錄（跟雲端 Project 同名檔案內容一致）。
- **本機 `HANDOFF_SUMMARY.md`**：這個 repo 目前的工作進度/已知限制/待辦事項（現況參考文件，不是逐輪決策紀錄）。
- **雲端 Project「Legency」**：`00_PROJECT_OVERVIEW.md`（含 EIBG_Assemble 專案實例與現況）、`02_TECH_ARCHITECTURE.md`（cas_robotic_ws 架構）、`04_EXAMPLES_PATTERNS.md`、`05_VERIFICATION_PLAN.md`（EIBG_Assemble 實機驗證追蹤表）— 大專案或需要完整現況/驗證紀錄時查閱。
