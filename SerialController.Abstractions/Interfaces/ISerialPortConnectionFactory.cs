using SerialController.Abstractions.Models;

namespace SerialController.Abstractions.Interfaces;

/// <summary>依一筆 <see cref="SerialPortConfig"/> 建立對應的 <see cref="ISerialPortConnection"/> 實例。</summary>
public interface ISerialPortConnectionFactory
{
    /// <summary>建立（但不會自動開啟）一個對應設定的連線實例，呼叫端要自己呼叫
    /// <see cref="ISerialPortConnection.OpenAsync"/>，理由同
    /// <c>AdamIoController.Abstractions.Interfaces.IAdamIoModuleFactory.Create</c>。</summary>
    ISerialPortConnection Create(SerialPortConfig config);
}
