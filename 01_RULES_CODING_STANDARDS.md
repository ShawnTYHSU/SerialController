# RULES.md - C# .NET 8 / WPF 重構守則

## 1. 核心原則 (Core Principles)
*   **Layer Separation (分層隔離)：** 永遠優先開發 Library (Class Library)。嚴禁在邏輯模組中出現 `System.Windows` 或任何 UI 相關命名空間。
*   **Dependency Inversion (依賴反轉)：** 模組內的外部操作（檔案、硬體、設定）必須依賴 `Interface`，透過 Constructor Injection 注入。
*   **Strong Typing (強力型別)：** Python 的動態資料結構必須轉化為 C# 的 `record`、`class` 或 `enum`。拒絕使用 `dynamic` 或過度依賴 `object`。
*   **Dynamic Conversion Rule：** 當轉換 Python 的動態 Dictionary 時，若結構複雜，請優先定義一個 `readonly record struct` 作為資料傳輸物件 (DTO)，並確保其型別安全。

## 2. 程式碼實作規範 (Implementation Standards)
*   **.NET 8 特性：**
    *   強制使用 `file-scoped namespaces`。
    *   開啟 `<Nullable>enable</Nullable>`，嚴格處理 Null 檢查。
    *   善用 C# 12+ 新語法（如 primary constructors, collection expressions）。
*   **模組與封裝：**
    *   所有商業邏輯必須封裝為 `Service` 或 `Manager` 類別。
    *   公開的 API 必須以 `Interface` 定義（例如：`public interface IProcessingService`）。
*   **非同步優先 (Async-First)：**
    *   所有可能造成阻塞的 I/O、運算或設備互動，一律使用 `async Task`。
    *   禁止使用 `.Result` 或 `.Wait()`。

## 3. MVVM 銜接預留 (WPF Preparedness)
*   雖然目前專注於 Library，但設計時請考量：
    *   **Notification：** 若模組有狀態需要即時回饋給 UI，請實作 `INotifyPropertyChanged` 或提供 `IObservable` (Reactive) 介面。
    *   **Data Models：** 確保資料模型層與業務邏輯層分離，便於未來在 ViewModel 中直接使用。

## 4. Claude 回應限制 (Interaction Rules)
*   **輸出前請先思考：** 對於複雜的重構邏輯，請先在 `<thought>` 標籤中簡述你的轉寫策略。
*   **預設測試：** 針對每一個 Service，請同步提供對應的測試結構建議（例如 xUnit 的測試類別骨架）。
*   **禁止事項：**
    *   嚴禁在 Library 程式碼中撰寫 `MessageBox.Show()`。
    *   嚴禁在 Library 邏輯中寫死配置路徑，應透過 `IOptions<T>` 或 `Configuration` 注入。
*   **回應格式：** 若涉及重構，請依序輸出：
    1.  **介面定義 (Interface)**
    2.  **核心邏輯實作 (Service Implementation)**
    3.  **單元測試建議 (Unit Test Framework)**
*   **輸出形式（Deliverable Format）：**
    *   任何**預期會被直接套用進專案**的程式碼（`.cs`/`.xaml`/`.csproj`/`.json` 設定檔等），一律建立為可在右側視窗開啟、可下載的檔案，不得只用聊天訊息中的 code block 呈現。
    *   純粹**說明用途**的程式碼片段（例如解釋某個 API 的用法、對比修改前後差異的節錄、示意某個概念但不預期被直接複製套用）不受此限，可以維持在聊天訊息中以 code block 呈現。
    *   若同一個回應中一個檔案有多處修改，優先建立/更新完整檔案，而不是要求使用者自行拼接多段片段。