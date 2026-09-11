using CommunityToolkit.Mvvm.ComponentModel;

namespace SerialController.Wpf.Controls;

/// <summary>
/// 單一 Holding Register 的顯示狀態，供 <see cref="ModbusSerialDeviceViewModel.HoldingRegisters"/>
/// 清單使用。純顯示用的資料容器，不含任何通訊邏輯（通訊邏輯在
/// <see cref="ModbusSerialDeviceViewModel"/>），跟
/// <c>AdamIoController.Wpf.Controls.AdamChannelViewModel</c> 是同一種角色分工。
/// </summary>
public sealed partial class RegisterViewModel : ObservableObject
{
    /// <summary>0-based 暫存器位址，畫面上顯示會是「#{Address}」。</summary>
    public int Address { get; }

    [ObservableProperty]
    private ushort _value;

    public RegisterViewModel(int address) => Address = address;
}
