using SerialController.Abstractions.Models;

namespace SerialController.Abstractions.Interfaces;

/// <summary>
/// 對「一個實體序列埠」的原始位元組收發抽象——刻意跟 <see cref="IModbusSerialDevice"/>
/// 是完全獨立的介面，不是後者的基礎介面：<c>SerialController.Protocols.NModbus</c> 裡的
/// Modbus 實作會自己管理內部的 <c>System.IO.Ports.SerialPort</c> + NModbus master（NModbus
/// 需要它自己的 <c>IStreamResource</c>/adapter，不是這個介面），兩者不強制耦合。
/// 這個介面存在的目的是讓「原始序列埠收發」本身是一個可重用的能力——如果之後有非
/// Modbus 的序列協定（例如廠商自訂的文字指令集），可以直接實作/重用這個介面，不需要
/// 跟著 Modbus 的語意走。
/// </summary>
public interface ISerialPortConnection : IAsyncDisposable
{
    /// <summary>這條連線的設定（唯讀，建構時決定，不能中途更換埠名/參數——要換設定
    /// 就整個物件重新建立，理由同 <c>AdamIoController</c> 系列的 Config 屬性設計）。</summary>
    SerialPortConfig Config { get; }

    /// <summary>目前埠是否已開啟。</summary>
    bool IsOpen { get; }

    /// <summary>最近一次 <see cref="OpenAsync"/> 失敗時的實際例外訊息，開啟成功則是 null。
    /// 只給診斷用，呼叫端不應該依賴這個字串的確切格式做邏輯判斷。</summary>
    string? LastError { get; }

    /// <summary>開啟序列埠，回傳是否成功。失敗不會拋例外（例如埠名打錯、埠已被其他
    /// 程式佔用），呼叫端看回傳值決定要不要重試/告知使用者，理由同
    /// <c>AdamIoController.Abstractions.Interfaces.IAdamIoModule.ConnectAsync</c>。</summary>
    Task<bool> OpenAsync(CancellationToken token = default);

    /// <summary>主動關閉連線，不拋例外（就算本來就沒開啟也安全）。</summary>
    void Close();

    /// <summary>寫入原始位元組。</summary>
    Task WriteAsync(byte[] data, CancellationToken token = default);

    /// <summary>讀取最多 <paramref name="maxBytes"/> 個位元組，回傳實際讀到的資料
    /// （長度可能小於 <paramref name="maxBytes"/>）。</summary>
    Task<byte[]> ReadAsync(int maxBytes, CancellationToken token = default);
}
