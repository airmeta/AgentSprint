using AgentSprint.Model.Modules.Agile.Workers;

namespace AgentSprint.Service.Services.AgileServices;

public interface IWorkerCommandLogBuffer
{
    /// <summary>
    /// zh-cn: 鎶婂懡浠ゆ棩蹇楀閲忓啓鍏ヨ繍琛屾椂缂撳啿鍖猴紝鐢ㄤ簬 Worker 姣?200ms 宸﹀彸鎺ㄩ€佸苟鏀寔绠＄悊绔洃鎺ц鍙栥€?
    /// en-us: Appends an incremental command-log chunk into the runtime buffer so Workers can push about every 200 ms and management views can read live output.
    /// </summary>
    WorkerCommandLogSnapshotResult Append(
        string workerId,
        string commandId,
        string? sessionId,
        string? runId,
        string instanceId,
        string? chunk,
        long sequence,
        bool completed);

    /// <summary>
    /// zh-cn: 鑾峰彇褰撳墠缂撳啿鍖轰腑鐨勫懡浠ゆ棩蹇楀揩鐓э紝鑻ョ紦鍐插尯宸茬粏鏉熷垯杩斿洖绌恒€?
    /// en-us: Gets the current buffered command-log snapshot; returns empty when the buffer has already ended or does not exist.
    /// </summary>
    WorkerCommandLogSnapshotResult? Get(string commandId);

    /// <summary>
    /// zh-cn: 绉婚櫎宸插啓鍏ユ暟鎹簱鎴栬繃鏈熺殑鍛戒护鏃ュ織缂撳啿銆?
    /// en-us: Removes a command-log buffer after it has been persisted or expired.
    /// </summary>
    void Remove(string commandId);
}
