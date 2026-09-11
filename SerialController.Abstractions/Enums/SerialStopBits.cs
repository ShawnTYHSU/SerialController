namespace SerialController.Abstractions.Enums;

/// <summary>序列埠停止位元數，理由同 <see cref="SerialParity"/>——不依賴 <c>System.IO.Ports.StopBits</c>。</summary>
public enum SerialStopBits
{
    One,
    OnePointFive,
    Two,
}
