# SerialController 技術交接文件

> **現狀參考文件**（系統現在長什麼樣子、有哪些已知限制、有哪些待辦事項），
> 不是逐輪決策紀錄。這是這個 repo 的第一份 HANDOFF_SUMMARY，之後每次工作
> 完再往下累加章節，不要整份重寫。

---

## 2026-09-11 工作項目

### 1. `\init`：建立初版 `CLAUDE.md`

當時 repo 只有 `\init` 產生的空白 WPF 骨架（`SerialController.Wpf.Controls`，
單純引用 `SwallowBase_DLL` 做主題資源），沒有任何應用邏輯。初版 `CLAUDE.md`
記錄了這個狀態、跨 repo 依賴（`SwallowBase` 必須是同層 sibling 目錄）、
`01_RULES_CODING_STANDARDS.md`/`03_GLOSSARY_AND_MAPPINGS.md` 的重構守則摘要。

### 2. 依 `AdamIoController` 架構＋`EIBG_Assemble` 消費情境，建置序列 Modbus 控制專案骨架

**需求**：把這個 repo 做成序列埠（SerialPort）控制專案，包含
`.Protocols.NModbus` 協定實作，專案結構至少要有 Abstractions/Core/Wpf.Controls。

**架構決策**（事先跟使用者確認過）：
- Abstractions 先做**原始序列埠傳輸層**（`ISerialPortConnection`）獨立於
  Modbus 語意之外，不是只做一個 Modbus 專用介面——理由：SerialController
  不像 `AdamIoController` 綁定單一裝置家族（ADAM），要能控制任意 Modbus
  從站裝置，之後也可能有非 Modbus 的序列協定需求。
- Modbus RTU 跟 ASCII **兩種訊框格式都支援**（`ModbusProtocolMode`），不是
  只做業界常見的 RTU。
- **暫不做 `TestApp.Console`**（`AdamIoController` 有一個對應的手動驗證
  工具，這次先跳過，之後有需要再補，見下方待辦）。

**新增/修改的專案**（五個 project，`net8.0-windows`，`Debug|x64`/`Release|x64`）：

| 專案 | 內容 |
|---|---|
| `SerialController.Abstractions` | `Enums`（`SerialParity`/`SerialStopBits`/`ModbusProtocolMode`）、`Models`（`SerialPortConfig`/`ModbusSerialDeviceConfig`，皆 `readonly record struct` + `CreateDefault`）、`Interfaces`（`ISerialPortConnection`/`ISerialPortConnectionFactory`/`IModbusSerialDevice`/`IModbusSerialDeviceFactory`）。 |
| `SerialController.Core` | `ModbusSerialDeviceRegistry`——`AdamIoModuleRegistry` 的結構化移植（名稱鍵值字典、依序 `ConnectAllAsync`、`RemoveDeviceAsync` 會先 Dispose 才移除）。 |
| `SerialController.Protocols.NModbus` | `SerialPortConnection`/`SerialPortConnectionFactory`（`ISerialPortConnection` 的 `System.IO.Ports.SerialPort` 實作）；`ModbusSerialDevice`/`ModbusSerialDeviceFactory`（`IModbusSerialDevice` 的 NModbus RTU/ASCII 實作，內部管理自己的 `SerialPort` + NModbus serial master，所有 master 存取都用 `SemaphoreSlim` 序列化，理由同 `AdamIoController.Protocols.Modbus.ModbusAdamIoModule._modbusLock`）；`SerialPortStreamResource`（見下方「技術筆記」）。 |
| `SerialController.Wpf.Controls`（既有專案，修改） | 新增 `RegisterViewModel`/`ModbusSerialDeviceViewModel`/`SerialDevicePanelViewModel`/`SerialDevicePanelView.xaml`，500ms 輪詢節奏（跟本系列專案既有慣例一致），只依賴 `Abstractions`+`Core`（不依賴 `Protocols.NModbus`，符合分層規則）。`MainWindow.xaml`/`App.xaml` 維持空白骨架，沒有接上實際 wiring（跟 `AdamIoController.Wpf.Controls` 目前實際狀態一致）。 |

`SerialController.sln` 同步加入三個新專案的 GUID/config 設定。`CLAUDE.md`
改寫為完整的分層架構說明（含從 `AdamIoController` 濃縮過來的跨專案分層
規則表格）。

### 3. 技術筆記——NModbus 3.0.83 序列埠支援的實際狀況

透過反射實際列舉已安裝的 `NModbus.dll`（3.0.83）公開型別確認（不是憑空
假設）：這個版本的 NModbus **沒有內建序列埠 adapter**（只有
`TcpClientAdapter`/`UdpClientAdapter`/`SocketAdapter`），跟原本以為的
`NModbus.IO.SerialPortAdapter` 不存在。解法：自己寫
`SerialPortStreamResource` 實作 NModbus 的 `IStreamResource` 介面包裝
`System.IO.Ports.SerialPort`（不擁有 `SerialPort` 生命週期，`Dispose()`
是空實作，開關埠是 `ModbusSerialDevice.Disconnect()` 的職責）。另外確認
`ModbusFactory.CreateRtuMaster`/`CreateAsciiMaster` 其實是 `NModbus`
命名空間下的擴充方法（`NModbus.FactoryExtensions`），不是 `ModbusFactory`
類別本身的方法，容易漏看。

### 4. 驗證狀態

- ✅ `dotnet build SerialController.sln -c Debug` / `-c Release`：五個專案
  全部建置成功，0 警告 0 錯誤。
- ❌ **沒有實體序列裝置可用，完全沒有實機驗證**——NModbus RTU/ASCII 的
  訊框行為（CRC/LRC 校驗、逾時處理是否符合現場裝置）目前只是編譯通過，
  行為本身未經任何測試確認。

---

## 待辦事項

1. 🔴 **實機驗證 NModbus RTU/ASCII 通訊**——目前完全沒有測過。第一次接
   實體裝置建議先用已知暫存器位址的簡單裝置（例如一顆現成的 RS-485
   電表/繼電器模組）驗證：連線是否成功、`ReadHoldingRegistersAsync`/
   `WriteSingleRegisterAsync` 讀寫是否正確、`SlaveId`/RTU vs ASCII 是否
   符合裝置實際設定。
2. 🟡 **`TestApp.Console`**——這次刻意跳過，之後有實體裝置要驗證時，
   比照 `AdamIoController.TestApp.Console` 做一個互動式命令列工具
   （連線 + `r` 讀取 + 手動寫入單一暫存器 + `q` 離開）。
3. 🟡 **RS-485 多裝置共用同一實體埠**——目前 `ModbusSerialDevice` 假設
   每台裝置獨占一個 `SerialPort`，如果現場是多個 SlaveId 共用一條線、
   同一個 COM 埠，目前的實作會互搶埠而失敗，需要「多個裝置共用同一個
   已開啟連線」的重新設計，目前沒有已知需求，故意沒做。
4. 🟡 **消費端整合（`EIBG_Assemble` 或其他 App）**——目前沒有任何 App
   實際引用這個 repo。之後要接的話，比照 `EIBG_Assemble` 消費
   `AdamIoController` 的模式：消費端自己的 `*Coordinator` 讀 JSON 設定檔
   建立裝置清單、注入 `ModbusSerialDeviceFactory`、持有
   `SerialDevicePanelViewModel` 生命週期、App 啟動時呼叫一次
   `ConnectAllAsync`。
5. 🟢 **`SerialDevicePanelViewModel.StatusMessage` 目前是純文字，沒有主題
   色分級**——`AdamIoController` 版本有 `ConnectionSummaryState`→
   `Brush-AdamIoPanelView-*` 主題資源鍵值的機制，這次因為
   `SwallowBase_DLL` 還沒有對應的 Color.xaml 鍵值，故意沒做。之後真的
   要做，需要先去 `SwallowBase_DLL` 補上對應資源鍵值。
