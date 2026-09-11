using SerialController.Abstractions.Models;

namespace SerialController.Abstractions.Interfaces;

/// <summary>
/// 對「一台透過序列埠掛載的 Modbus 從站裝置」的讀寫抽象。刻意用通用的 Modbus 功能碼
/// 層級方法（讀/寫 Coils、Discrete Inputs、Holding/Input Registers），不是像
/// <c>AdamIoController.Abstractions.Interfaces.IAdamIoModule</c> 那樣針對單一裝置家族
/// 曝露 DI/DO/AI 專屬方法——SerialController 不綁定單一裝置型號，消費端要能控制任意
/// Modbus RTU/ASCII 從站裝置，型號/暫存器位址對照表的知識留在消費端（呼叫端自己決定
/// 要讀哪個位址、怎麼解讀讀回來的原始值）。
///
/// ⚠️ 目前設計刻意保持單純——每個方法對應一次 Modbus 交易，沒有做批次快取/合併多個
/// 讀取請求成一次交易這種最佳化，理由同 IAdamIoModule 文件註解。
/// </summary>
public interface IModbusSerialDevice : IAsyncDisposable
{
    /// <summary>這台裝置的連線設定（唯讀，建構時決定）。</summary>
    ModbusSerialDeviceConfig Config { get; }

    /// <summary>目前是否已開啟序列埠。連線中斷不會自動讓這個屬性即時更新，只有下一次
    /// 讀寫操作實際失敗時才會被動發現，理由同 IAdamIoModule.IsConnected。</summary>
    bool IsConnected { get; }

    /// <summary>最近一次 <see cref="ConnectAsync"/> 失敗時的實際例外訊息，連線成功則是 null。
    /// 只給診斷用，呼叫端不應該依賴這個字串的確切格式做邏輯判斷。</summary>
    string? LastError { get; }

    /// <summary>開啟序列埠並準備好 Modbus master，回傳是否成功。失敗不會拋例外
    /// （例如埠名打錯、埠被佔用、裝置沒上電），理由同 IAdamIoModule.ConnectAsync。</summary>
    Task<bool> ConnectAsync(CancellationToken token = default);

    /// <summary>主動關閉連線，不拋例外（就算本來就沒連線也安全）。</summary>
    void Disconnect();

    /// <summary>讀取 Coils（FC01，讀寫型數位輸出點）。</summary>
    Task<bool[]> ReadCoilsAsync(ushort startAddress, ushort count, CancellationToken token = default);

    /// <summary>讀取 Discrete Inputs（FC02，唯讀型數位輸入點）。</summary>
    Task<bool[]> ReadDiscreteInputsAsync(ushort startAddress, ushort count, CancellationToken token = default);

    /// <summary>讀取 Holding Registers（FC03，讀寫型 16-bit 暫存器）。</summary>
    Task<ushort[]> ReadHoldingRegistersAsync(ushort startAddress, ushort count, CancellationToken token = default);

    /// <summary>讀取 Input Registers（FC04，唯讀型 16-bit 暫存器）。</summary>
    Task<ushort[]> ReadInputRegistersAsync(ushort startAddress, ushort count, CancellationToken token = default);

    /// <summary>寫入單一 Coil（FC05）。</summary>
    Task WriteSingleCoilAsync(ushort address, bool value, CancellationToken token = default);

    /// <summary>寫入單一 Holding Register（FC06）。</summary>
    Task WriteSingleRegisterAsync(ushort address, ushort value, CancellationToken token = default);

    /// <summary>連續寫入多個 Coils（FC15）。</summary>
    Task WriteMultipleCoilsAsync(ushort startAddress, bool[] values, CancellationToken token = default);

    /// <summary>連續寫入多個 Holding Registers（FC16）。</summary>
    Task WriteMultipleRegistersAsync(ushort startAddress, ushort[] values, CancellationToken token = default);
}
